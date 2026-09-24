using System.Text;
using BiteShare.Api.Hubs;
using BiteShare.Api.Services;
using BiteShare.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---------------------------------------------------------

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Paste an identity or participant JWT (no 'Bearer ' prefix needed here)."
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddSignalR();

// Hosts like Render provide DATABASE_URL (postgres://user:pass@host:port/db); locally use ConnectionStrings:Default.
var connectionString = ToNpgsqlConnectionString(Environment.GetEnvironmentVariable("DATABASE_URL"))
                       ?? builder.Configuration.GetConnectionString("Default");

builder.Services.AddDbContext<BiteShareDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<BiteShareDbContext>()
    .AddDefaultTokenProviders();

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"] ?? builder.Configuration["Jwt:SigningKey"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? "dev-only-placeholder-key-replace-me-1234567890")),
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    // SignalR's browser client can't set an Authorization header on the WebSocket
    // handshake, so it sends the token as ?access_token=... instead — accept that
    // for hub requests only.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("IdentityOnly", policy => policy.RequireClaim(BiteShareClaimTypes.TokenType, "identity"))
    .AddPolicy("ParticipantOnly", policy => policy.RequireClaim(BiteShareClaimTypes.TokenType, "participant"));

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IJoinCodeGenerator, JoinCodeGenerator>();
builder.Services.AddScoped<ISplitterService, SplitterService>();
builder.Services.AddScoped<IStripePaymentService, StripePaymentService>();
builder.Services.AddScoped<IReceiptPdfService, ReceiptPdfService>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientPolicy", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()); // required for SignalR
});

QuestPDF.Settings.License = LicenseType.Community;
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

var app = builder.Build();

// Create/upgrade the schema on startup so a fresh deploy needs no manual `dotnet ef` step.
// (Skipped for non-relational providers, e.g. the in-memory DB used by tests.)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BiteShareDbContext>();
    if (db.Database.IsRelational())
        db.Database.Migrate();
}

// --- Pipeline -----------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("ClientPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = new FileExtensionContentTypeProvider
    {
        Mappings =
        {
            [".dat"] = "application/octet-stream"
        }
    }
});

app.MapControllers();
app.MapHub<OrderHub>("/hubs/order");
app.MapFallbackToFile("index.html");

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

static string? ToNpgsqlConnectionString(string? url)
{
    if (string.IsNullOrWhiteSpace(url)) return null;
    if (!url.StartsWith("postgres://") && !url.StartsWith("postgresql://")) return url; // already key=value form

    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':', 2);
    var port = uri.Port > 0 ? uri.Port : 5432;
    return $"Host={uri.Host};Port={port};Database={uri.AbsolutePath.TrimStart('/')};" +
           $"Username={Uri.UnescapeDataString(userInfo[0])};Password={Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? "")};" +
           "SSL Mode=Prefer;Trust Server Certificate=true";
}

// Exposed so integration tests can host the API with WebApplicationFactory.
public partial class Program { }

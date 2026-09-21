using BiteShare.Api.Services;
using BiteShare.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<BiteShareDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ISplitterService, SplitterService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApp", policy =>
    {
        policy.WithOrigins(builder.Configuration["ClientAppUrl"] ?? "https://localhost:5001")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("ClientApp");
app.UseAuthorization();
app.MapControllers();

// TODO(Aaron): map OrderHub once it exists - app.MapHub<OrderHub>("/hubs/order");

app.Run();

// Exposed so integration tests can spin the API up in-memory via WebApplicationFactory<Program>.
public partial class Program { }

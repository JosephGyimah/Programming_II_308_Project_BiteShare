using BiteShare.Client;
using BiteShare.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:5001";

builder.Services.AddSingleton<AuthTokenStore>();
builder.Services.AddTransient<IdentityAuthHandler>();
builder.Services.AddTransient<ParticipantAuthHandler>();

// Two named clients: one attaches the identity JWT (account-level calls like
// creating a session), the other attaches the participant JWT (cart/orders/
// participants/menu — everything scoped to the session someone has joined).
builder.Services.AddHttpClient("IdentityApi", c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<IdentityAuthHandler>();
builder.Services.AddHttpClient("SessionApi", c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<ParticipantAuthHandler>();

builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<OrderHubService>();

await builder.Build().RunAsync();

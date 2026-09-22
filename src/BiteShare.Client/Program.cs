using BiteShare.Client;
using BiteShare.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
                 ?? "https://localhost:5001";

builder.Services.AddHttpClient("IdentityApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

builder.Services.AddHttpClient("SessionApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

builder.Services.AddScoped<AuthTokenStore>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<OrderHubService>();

await builder.Build().RunAsync();

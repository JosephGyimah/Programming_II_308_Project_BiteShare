using BiteShare.Client;
using BiteShare.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");

// Blank ApiBaseUrl (production) = same origin: the API serves this client, so use the page's own address.
var configuredBaseUrl = builder.Configuration["ApiBaseUrl"];
var apiBaseUrl = (string.IsNullOrWhiteSpace(configuredBaseUrl) ? builder.HostEnvironment.BaseAddress : configuredBaseUrl).TrimEnd('/');
builder.Configuration["ApiBaseUrl"] = apiBaseUrl; // OrderHubService reads the same key

builder.Services.AddSingleton<AuthTokenStore>();
builder.Services.AddTransient<IdentityAuthHandler>();
builder.Services.AddTransient<ParticipantAuthHandler>();

builder.Services.AddHttpClient("IdentityApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<IdentityAuthHandler>();

builder.Services.AddHttpClient("SessionApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<ParticipantAuthHandler>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<OrderHubService>();

await builder.Build().RunAsync();

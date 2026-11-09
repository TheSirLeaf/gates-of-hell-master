using GatesOfHell.Master;
using GatesOfHell.Shared.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient());
builder.Services.AddScoped<AppSettings>();
builder.Services.AddScoped<GameApiClient>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<GameSyncService>();

await builder.Build().RunAsync();

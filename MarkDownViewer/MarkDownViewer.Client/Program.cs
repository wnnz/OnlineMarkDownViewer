using MarkDownViewer.Client;
using MarkDownViewer.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});
builder.Services.AddScoped<ClientAuthService>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<MarkdownRendererService>();

await builder.Build().RunAsync();

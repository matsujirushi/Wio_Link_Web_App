using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using MudBlazor.Services;
using WioLinkWebApp;
using WioLinkWebApp.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddHttpClient("WioServerApi", client =>
{
    client.BaseAddress = new Uri("https://wiolink.seeed.co.jp/v1/");
});

builder.Services.AddScoped<WioLinkService>();
builder.Services.AddMudServices();

var host = builder.Build();
var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
var cultureName = await jsRuntime.InvokeAsync<string>("getBrowserCulture");
var culture = CultureInfo.GetCultureInfo(cultureName);

CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

await host.RunAsync();

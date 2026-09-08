using System.Net.Http;
using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using WioLinkWebApp.Pages;
using WioLinkWebApp.Services;
using Xunit;

namespace WioLinkWebApp.Tests.Pages;

public class LoginTests : TestContext
{
    [Fact]
    public void CustomServerInput_UsesProductionUrlPlaceholder_AndOmitsExampleText()
    {
        Services.AddScoped<IHttpClientFactory, StubHttpClientFactory>();
        Services.AddScoped<WioLinkService>();

        var cut = RenderComponent<Login>();

        var serverType = cut.Find("#serverType");
        serverType.Change("Custom");

        var serverUrlInput = cut.Find("#serverUrl");

        Assert.Equal("https://wiolink.seeed.co.jp", serverUrlInput.GetAttribute("placeholder"));
        Assert.DoesNotContain("��: https://wiolink.seeed.co.jp", cut.Markup);
    }

    [Fact]
    public void DeviceList_ShowsJapaneseServerName_ForJapanServer()
    {
        Services.AddScoped<IHttpClientFactory, StubHttpClientFactory>();
        Services.AddScoped<WioLinkService>();

        var service = Services.GetRequiredService<WioLinkService>();
        SetProperty(service, nameof(WioLinkService.AccessToken), "test-token");
        SetProperty(service, nameof(WioLinkService.ServerBaseAddress), "https://wiolink.seeed.co.jp/");

        var cut = RenderComponent<DeviceList>();

        Assert.Contains("接続先: 日本", cut.Markup);
    }

    [Fact]
    public void DeviceList_ShowsCustomUrl_ForCustomServer()
    {
        Services.AddScoped<IHttpClientFactory, StubHttpClientFactory>();
        Services.AddScoped<WioLinkService>();

        var service = Services.GetRequiredService<WioLinkService>();
        SetProperty(service, nameof(WioLinkService.AccessToken), "test-token");
        SetProperty(service, nameof(WioLinkService.ServerBaseAddress), "https://example.com/");

        var cut = RenderComponent<DeviceList>();

        Assert.Contains("接続先: https://example.com", cut.Markup);
    }

    private static void SetProperty<T>(T target, string propertyName, object value)
    {
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}

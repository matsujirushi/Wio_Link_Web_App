using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
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

    [Fact]
    public void DeviceConfigWioNode_ShowsSelectedDeviceName()
    {
        Services.AddScoped<IHttpClientFactory, TestWioHttpClientFactory>();
        Services.AddScoped<WioLinkService>();

        var service = Services.GetRequiredService<WioLinkService>();
        SetProperty(service, nameof(WioLinkService.AccessToken), "test-token");
        SetProperty(service, nameof(WioLinkService.ServerBaseAddress), "https://wiolink.seeed.co.jp/");

        var cut = RenderComponent<DeviceConfigWioNode>(parameters => parameters.Add(p => p.NodeSn, "NODE-123"));

        Assert.Contains("Wio Node - Test Device", cut.Markup);
    }

    [Fact]
    public void DeviceConfigWioNode_DisablesFirmwareUpdateUntilConnectorChanges()
    {
        Services.AddScoped<IHttpClientFactory, TestWioHttpClientFactory>();
        Services.AddScoped<WioLinkService>();

        var service = Services.GetRequiredService<WioLinkService>();
        SetProperty(service, nameof(WioLinkService.AccessToken), "test-token");
        SetProperty(service, nameof(WioLinkService.ServerBaseAddress), "https://wiolink.seeed.co.jp/");

        var cut = RenderComponent<DeviceConfigWioNode>(parameters => parameters.Add(p => p.NodeSn, "NODE-123"));
        var updateButton = cut.Find("button.btn-primary");

        Assert.True(updateButton.HasAttribute("disabled"));

        cut.Find(".grove-module").TriggerEvent("ondragstart", new DragEventArgs());
        cut.FindAll(".wio-connector")[0].TriggerEvent("ondrop", new DragEventArgs());

        Assert.False(cut.Find("button.btn-primary").HasAttribute("disabled"));

        cut.Find(".connector-item").TriggerEvent("ondragstart", new DragEventArgs());
        cut.Find(".grove-palette").TriggerEvent("ondrop", new DragEventArgs());

        Assert.True(cut.Find("button.btn-primary").HasAttribute("disabled"));
    }

    [Fact]
    public async Task DeviceConfigWioNode_DisablesButton_WhenOnDeviceConfigMatchesAfterFirmwareUpdate()
    {
        var handler = new StubWioHttpMessageHandler();
        Services.AddScoped<IHttpClientFactory>(_ => new TestWioHttpClientFactory(handler));
        Services.AddScoped<WioLinkService>();

        var service = Services.GetRequiredService<WioLinkService>();
        SetProperty(service, nameof(WioLinkService.AccessToken), "test-token");
        SetProperty(service, nameof(WioLinkService.ServerBaseAddress), "https://wiolink.seeed.co.jp/");

        var cut = RenderComponent<DeviceConfigWioNode>(parameters => parameters.Add(p => p.NodeSn, "NODE-123"));

        cut.Find(".grove-module").TriggerEvent("ondragstart", new DragEventArgs());
        cut.FindAll(".wio-connector")[0].TriggerEvent("ondrop", new DragEventArgs());
        Assert.False(cut.Find("button.btn-primary").HasAttribute("disabled"));

        // OTA完了後、実機側の構成取得APIが画面上の構成と一致する値を返すケース。
        handler.NodeConfigConnections = "[{\"port\":\"D0\",\"sku\":\"GROVE-1\"}]";
        handler.OtaStatus = "done";

        await cut.InvokeAsync(() => cut.Find("button.btn-primary").Click());
        cut.WaitForAssertion(() => Assert.True(cut.Find("button.btn-primary").HasAttribute("disabled")), TimeSpan.FromSeconds(2));
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

    private sealed class TestWioHttpClientFactory : IHttpClientFactory
    {
        private readonly StubWioHttpMessageHandler handler;

        public TestWioHttpClientFactory()
            : this(new StubWioHttpMessageHandler())
        {
        }

        public TestWioHttpClientFactory(StubWioHttpMessageHandler handler)
        {
            this.handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler);
        }
    }

    private sealed class StubWioHttpMessageHandler : HttpMessageHandler
    {
        public string NodeConfigConnections { get; set; } = "[]";
        public string OtaStatus { get; set; } = "done";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.AbsoluteUri ?? string.Empty;

            if (uri.Contains("/v1/nodes/list"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"nodes\":[{\"node_key\":\"NODE_KEY_123\",\"node_sn\":\"NODE-123\",\"name\":\"Test Device\",\"board\":\"Wio Node v1.0\",\"online\":true}]}",
                        Encoding.UTF8,
                        "application/json")
                });
            }

            if (uri.Contains("/v1/scan/drivers"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"drivers\":[{\"GroveName\":\"Test GPIO Module\",\"SKU\":\"GROVE-1\",\"ImageURL\":\"\",\"InterfaceType\":\"GPIO\"}]}",
                    Encoding.UTF8,
                    "application/json")
                });
            }

            if (uri.Contains("/v1/node/config"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"connections\":{NodeConfigConnections}}}", Encoding.UTF8, "application/json")
                });
            }

            if (uri.Contains("/v1/ota/trigger"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                });
            }

            if (uri.Contains("/v1/ota/status"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"ota_status\":\"{OtaStatus}\"}}", Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}

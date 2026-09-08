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
    public void Login_ShowsSignUpButtonInHeader()
    {
        Services.AddScoped<IHttpClientFactory, StubHttpClientFactory>();
        Services.AddScoped<WioLinkService>();

        var cut = RenderComponent<Login>();

        var signUpButton = cut.Find(".d-flex button.btn-outline-primary");

        Assert.Equal("サインアップ", signUpButton.TextContent);
    }

    [Fact]
    public void SignUpPage_RendersServerAndCredentialsFields()
    {
        Services.AddScoped<IHttpClientFactory, StubHttpClientFactory>();
        Services.AddScoped<WioLinkService>();

        var cut = RenderComponent<Signup>();

        Assert.Contains("サインアップ", cut.Markup);
        Assert.Equal("戻る", cut.Find(".d-flex button.btn-outline-primary").TextContent);
        Assert.Contains("Wio Link サーバー", cut.Markup);
        Assert.Contains("Eメールアドレス", cut.Markup);
        Assert.Contains("パスワード", cut.Markup);
    }

    [Fact]
    public async Task SignUpAsync_UsesCreateUserEndpoint()
    {
        var handler = new StubWioHttpMessageHandler();
        Services.AddScoped<IHttpClientFactory>(_ => new TestWioHttpClientFactory(handler));
        Services.AddScoped<WioLinkService>();

        var service = Services.GetRequiredService<WioLinkService>();

        var result = await service.SignUpAsync("test@example.com", "password", "https://wiolink.seeed.co.jp");

        Assert.True(result.Success);
        Assert.Equal("https://wiolink.seeed.co.jp/v1/user/create", handler.LastRequestUri);
        Assert.Equal("application/x-www-form-urlencoded", handler.LastRequestContentType);
        Assert.Equal("email=test%40example.com&password=password", handler.LastRequestBody);
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
    public void DeviceList_Logout_ClearsAuthenticationAndNavigatesToLogin()
    {
        Services.AddScoped<IHttpClientFactory, StubHttpClientFactory>();
        Services.AddScoped<WioLinkService>();

        var service = Services.GetRequiredService<WioLinkService>();
        SetProperty(service, nameof(WioLinkService.AccessToken), "test-token");
        SetProperty(service, nameof(WioLinkService.ServerBaseAddress), "https://example.com/");

        var cut = RenderComponent<DeviceList>();

        cut.Find("button.btn-outline-primary").Click();

        Assert.False(service.IsLoggedIn);
        Assert.Null(service.ServerBaseAddress);
        Assert.EndsWith("/login", Services.GetRequiredService<NavigationManager>().Uri);
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

        Assert.Contains("デバイス - Test Device", cut.Markup);
    }

    [Fact]
    public void DeviceResources_ShowsSelectedDeviceNameInTitle()
    {
        Services.AddScoped<IHttpClientFactory, TestWioHttpClientFactory>();
        Services.AddScoped<WioLinkService>();

        var service = Services.GetRequiredService<WioLinkService>();
        SetProperty(service, nameof(WioLinkService.AccessToken), "test-token");
        SetProperty(service, nameof(WioLinkService.ServerBaseAddress), "https://wiolink.seeed.co.jp/");

        var cut = RenderComponent<DeviceResources>(parameters => parameters.Add(p => p.NodeSn, "NODE-123"));

        Assert.Contains("リソース - Test Device", cut.Markup);
    }

    [Fact]
    public async Task DeviceConfigWioNode_RenamesDevice()
    {
        var handler = new StubWioHttpMessageHandler();
        Services.AddScoped<IHttpClientFactory>(_ => new TestWioHttpClientFactory(handler));
        Services.AddScoped<WioLinkService>();

        var service = Services.GetRequiredService<WioLinkService>();
        SetProperty(service, nameof(WioLinkService.AccessToken), "test-token");
        SetProperty(service, nameof(WioLinkService.ServerBaseAddress), "https://wiolink.seeed.co.jp/");

        var cut = RenderComponent<DeviceConfigWioNode>(parameters => parameters.Add(p => p.NodeSn, "NODE-123"));
        cut.Find("button.btn-outline-primary").Click();
        cut.Find("#device-name").Change("Renamed Device");

        await cut.InvokeAsync(() => cut.Find(".modal-footer .btn-primary").Click());

        Assert.Equal("NODE-123", handler.RenamedNodeSn);
        Assert.Equal("Renamed Device", handler.RenamedNodeName);
        Assert.Contains("デバイス - Renamed Device", cut.Markup);
        Assert.DoesNotContain("id=\"rename-dialog-title\"", cut.Markup);
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
    public void DeviceConfigWioNode_RendersSectionsInRequestedOrder()
    {
        Services.AddScoped<IHttpClientFactory, TestWioHttpClientFactory>();
        Services.AddScoped<WioLinkService>();

        var service = Services.GetRequiredService<WioLinkService>();
        SetProperty(service, nameof(WioLinkService.AccessToken), "test-token");
        SetProperty(service, nameof(WioLinkService.ServerBaseAddress), "https://wiolink.seeed.co.jp/");

        var cut = RenderComponent<DeviceConfigWioNode>(parameters => parameters.Add(p => p.NodeSn, "NODE-123"));
        var markup = cut.Markup;

        Assert.True(markup.IndexOf("device-config-messages", StringComparison.Ordinal)
            < markup.IndexOf("btn-primary", StringComparison.Ordinal));
        Assert.True(markup.IndexOf("btn-primary", StringComparison.Ordinal)
            < markup.IndexOf("wio-board-stage", StringComparison.Ordinal));
        Assert.True(markup.IndexOf("wio-board-stage", StringComparison.Ordinal)
            < markup.IndexOf("Groveモジュール", StringComparison.Ordinal));
        Assert.Single(cut.FindAll(".card-header"));
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
        public string? RenamedNodeSn { get; private set; }
        public string? RenamedNodeName { get; private set; }
        public string? LastRequestUri { get; private set; }
        public string? LastRequestContentType { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.AbsoluteUri ?? string.Empty;
            LastRequestUri = uri;
            LastRequestContentType = request.Content?.Headers.ContentType?.MediaType;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            if (uri.Contains("/v1/user/create"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"result\":\"ok\"}", Encoding.UTF8, "application/json")
                };
            }

            if (uri.Contains("/v1/nodes/rename"))
            {
                var form = await request.Content!.ReadAsStringAsync(cancellationToken);
                foreach (var pair in form.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = pair.Split('=', 2);
                    var value = parts.Length == 2 ? Uri.UnescapeDataString(parts[1].Replace("+", " ", StringComparison.Ordinal)) : string.Empty;
                    if (parts[0] == "node_sn")
                    {
                        RenamedNodeSn = value;
                    }
                    else if (parts[0] == "name")
                    {
                        RenamedNodeName = value;
                    }
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"result\":\"ok\"}", Encoding.UTF8, "application/json")
                };
            }

            if (uri.Contains("/v1/nodes/list"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"nodes\":[{\"node_key\":\"NODE_KEY_123\",\"node_sn\":\"NODE-123\",\"name\":\"Test Device\",\"board\":\"Wio Node v1.0\",\"online\":true}]}",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            if (uri.Contains("/v1/scan/drivers"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"drivers\":[{\"GroveName\":\"Test GPIO Module\",\"SKU\":\"GROVE-1\",\"ImageURL\":\"\",\"InterfaceType\":\"GPIO\"}]}",
                    Encoding.UTF8,
                    "application/json")
                };
            }

            if (uri.Contains("/v1/node/resources"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<html><body>Resource Content</body></html>", Encoding.UTF8, "text/html")
                };
            }

            if (uri.Contains("/v1/node/config"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"connections\":{NodeConfigConnections}}}", Encoding.UTF8, "application/json")
                };
            }

            if (uri.Contains("/v1/ota/trigger"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            }

            if (uri.Contains("/v1/ota/status"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"ota_status\":\"{OtaStatus}\"}}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}

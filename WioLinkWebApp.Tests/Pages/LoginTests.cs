using System.Net.Http;
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

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}

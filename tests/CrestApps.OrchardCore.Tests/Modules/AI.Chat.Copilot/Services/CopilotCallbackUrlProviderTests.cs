using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Copilot.Services;

public sealed class CopilotCallbackUrlProviderTests
{
    [Fact]
    public void BuildSiteAbsoluteUrl_WhenSiteBaseUrlContainsTenantPrefix_ShouldUseConfiguredBaseUrl()
    {
        var siteBaseUri = new Uri("https://hs5g0sj3-44300.usw3.devtunnels.ms/blog1", UriKind.Absolute);
        var requestUri = new Uri("https://localhost:44300/blog1/copilot/OAuthCallback", UriKind.Absolute);

        var result = CopilotCallbackUrlProvider.BuildSiteAbsoluteUrl(siteBaseUri, requestUri, new PathString("/blog1"));

        Assert.Equal("https://hs5g0sj3-44300.usw3.devtunnels.ms/blog1/copilot/OAuthCallback", result.AbsoluteUri);
    }

    [Fact]
    public void BuildSiteAbsoluteUrl_WhenSiteBaseUrlHasNoTenantPrefix_ShouldKeepCallbackPath()
    {
        var siteBaseUri = new Uri("https://example.com", UriKind.Absolute);
        var requestUri = new Uri("https://localhost:44300/blog1/copilot/OAuthCallback?state=__popup__", UriKind.Absolute);

        var result = CopilotCallbackUrlProvider.BuildSiteAbsoluteUrl(siteBaseUri, requestUri, new PathString("/blog1"));

        Assert.Equal("https://example.com/copilot/OAuthCallback?state=__popup__", result.AbsoluteUri);
    }
}

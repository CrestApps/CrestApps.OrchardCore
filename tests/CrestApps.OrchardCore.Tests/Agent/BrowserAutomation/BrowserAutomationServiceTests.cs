using CrestApps.OrchardCore.AI.Agent;
using CrestApps.OrchardCore.AI.Agent.Services;
using Microsoft.Playwright;
using Microsoft.AspNetCore.Http;
using Moq;

namespace CrestApps.OrchardCore.Tests.Agent.BrowserAutomation;

public sealed class BrowserAutomationServiceTests
{
    [Fact]
    public void ResolveRequestedSessionId_WhenDefaultAliasAndNoSessions_ShouldThrowHelpfulError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BrowserAutomationService.ResolveRequestedSessionId(AgentConstants.DefaultSessionId, []));

        Assert.Contains("startBrowserSession", exception.Message, StringComparison.Ordinal);
        Assert.Contains("default", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveRequestedSessionId_WhenDefaultAlias_ShouldReturnMostRecentlyTouchedSession()
    {
        var oldest = CreateSession("oldest", new DateTime(2026, 03, 20, 12, 00, 00, DateTimeKind.Utc));
        var newest = CreateSession("newest", new DateTime(2026, 03, 20, 13, 00, 00, DateTimeKind.Utc));

        var actual = BrowserAutomationService.ResolveRequestedSessionId(AgentConstants.DefaultSessionId, [oldest, newest]);

        Assert.Equal("newest", actual);
    }

    [Fact]
    public void ResolveRequestedSessionId_WhenExplicitSessionId_ShouldReturnItUnchanged()
    {
        var actual = BrowserAutomationService.ResolveRequestedSessionId("my-session", []);

        Assert.Equal("my-session", actual);
    }

    [Fact]
    public void ResolveBootstrapUrl_WhenParentPageUrlExists_ShouldPreferIt()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?browserPageUrl=https%3A%2F%2Fexample.com%2Fchat&browserParentPageUrl=https%3A%2F%2Fexample.com%2Fadmin%2Fsearch%2Findexes");

        var actual = BrowserAutomationService.ResolveBootstrapUrl(httpContext);

        Assert.Equal("https://example.com/admin/search/indexes", actual);
    }

    [Fact]
    public void ResolveBootstrapUrl_WhenParentPageUrlMissing_ShouldUsePageUrl()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?browserPageUrl=https%3A%2F%2Fexample.com%2Fchat");

        var actual = BrowserAutomationService.ResolveBootstrapUrl(httpContext);

        Assert.Equal("https://example.com/chat", actual);
    }

    [Fact]
    public void ResolveBootstrapUrl_WhenQueryMissing_ShouldFallBackToReferer()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Referer = "https://example.com/admin/content";

        var actual = BrowserAutomationService.ResolveBootstrapUrl(httpContext);

        Assert.Equal("https://example.com/admin/content", actual);
    }

    private static BrowserAutomationSession CreateSession(string sessionId, DateTime lastTouchedUtc)
    {
        var session = new BrowserAutomationSession(
            sessionId,
            "chromium",
            true,
            Mock.Of<IPlaywright>(),
            Mock.Of<IBrowser>(),
            Mock.Of<IBrowserContext>(),
            new DateTime(2026, 03, 20, 11, 00, 00, DateTimeKind.Utc));

        session.Touch(lastTouchedUtc);

        return session;
    }
}

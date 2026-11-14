using System.Security.Claims;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

/// <summary>
/// Low (AI) of the final security review: a caller-checked tool reads the request's user, so post-session analysis
/// re-run by an API caller, a workflow or a close in someone's request ran the visitor's planted text with that person's
/// rights. The analysis now runs as no one, as the background close task already did.
/// </summary>
public sealed class PostSessionCallerTests
{
    [Fact]
    public async Task RunAsNoOneAsync_HidesTheRequestsUser_FromTheAnalysis_AndPutsItBack()
    {
        // Arrange
        var administrator = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin")], "Test"));
        var httpContext = new DefaultHttpContext { User = administrator };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        ClaimsPrincipal seenByTool = null;

        // Act: the tool reads the user the way caller-checked tools do.
        await PostSessionCaller.RunAsNoOneAsync(httpContext, () =>
        {
            seenByTool = accessor.HttpContext.User;

            return Task.CompletedTask;
        });

        // Assert
        Assert.False(seenByTool.Identity?.IsAuthenticated ?? false);
        Assert.Null(seenByTool.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Same(administrator, httpContext.User);
    }

    [Fact]
    public async Task RunAsNoOneAsync_PutsTheUserBack_WhenTheAnalysisFails()
    {
        var administrator = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin")], "Test"));
        var httpContext = new DefaultHttpContext { User = administrator };

        await Assert.ThrowsAsync<InvalidOperationException>(() => PostSessionCaller.RunAsNoOneAsync(httpContext, () => throw new InvalidOperationException()));

        Assert.Same(administrator, httpContext.User);
    }

    [Fact]
    public async Task RunAsNoOneAsync_OutsideARequest_RunsTheAnalysis()
    {
        var ran = false;

        await PostSessionCaller.RunAsNoOneAsync(null, () =>
        {
            ran = true;

            return Task.CompletedTask;
        });

        Assert.True(ran);
    }
}

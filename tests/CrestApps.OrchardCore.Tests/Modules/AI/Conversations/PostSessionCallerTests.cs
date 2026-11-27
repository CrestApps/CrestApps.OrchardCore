using System.Security.Claims;
using CrestApps.Core.Security;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

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
        await PostSessionCaller.RunAsNoOneAsync(httpContext, null, () =>
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
    public async Task RunAsNoOneAsync_HidesTheUserAccessorsUser_WhichToolsReadFirst()
    {
        // Arrange (round 3, Low): a hub publishes its caller through the user accessor, which caller-checked tools read
        // before the request's user, so replacing only the request's user left the caller's rights in the analysis.
        var administrator = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin")], "Test"));
        var userAccessor = new UserAccessor(new HttpContextAccessor());
        userAccessor.User = administrator;
        using var services = new ServiceCollection()
            .AddSingleton<IUserAccessor>(userAccessor)
            .AddAllowingAuthorization()
            .BuildServiceProvider();
        var allowed = true;

        // Act: no request, as in a hub invocation over a transport that leaves HttpContext empty.
        await PostSessionCaller.RunAsNoOneAsync(null, userAccessor, async () =>
        {
            allowed = await AIToolCallerAuthorization.IsAuthorizedAsync(services, new Permission("AnyToolPermission"));
        });

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public async Task RunAsNoOneAsync_PutsTheUserBack_WhenTheAnalysisFails()
    {
        var administrator = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin")], "Test"));
        var httpContext = new DefaultHttpContext { User = administrator };

        await Assert.ThrowsAsync<InvalidOperationException>(() => PostSessionCaller.RunAsNoOneAsync(httpContext, null, () => throw new InvalidOperationException()));

        Assert.Same(administrator, httpContext.User);
    }

    [Fact]
    public async Task RunAsNoOneAsync_OutsideARequest_RunsTheAnalysis()
    {
        var ran = false;

        await PostSessionCaller.RunAsNoOneAsync(null, null, () =>
        {
            ran = true;

            return Task.CompletedTask;
        });

        Assert.True(ran);
    }
}

using System.Security.Claims;
using CrestApps.Core.AI.Handlers;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Memory.Handlers;

public sealed class AIMemoryOrchestrationContextHelperTests
{
    [Fact]
    public void GetAuthenticatedUserId_WhenNameIdentifierExists_ShouldReturnIt()
    {
        var accessor = CreateAccessor(
            new Claim(ClaimTypes.NameIdentifier, "user-id"),
            new Claim(ClaimTypes.Name, "admin"));

        var userId = AIMemoryOrchestrationContextHelper.GetAuthenticatedUserId(accessor);

        Assert.Equal("user-id", userId);
    }

    [Fact]
    public void GetAuthenticatedUserId_WhenOnlyNameExists_ShouldFallbackToName()
    {
        var accessor = CreateAccessor(new Claim(ClaimTypes.Name, "admin"));

        var userId = AIMemoryOrchestrationContextHelper.GetAuthenticatedUserId(accessor);

        Assert.Equal("admin", userId);
    }

    private static HttpContextAccessor CreateAccessor(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(claims, "Cookies"));

        return new HttpContextAccessor
        {
            HttpContext = httpContext,
        };
    }
}

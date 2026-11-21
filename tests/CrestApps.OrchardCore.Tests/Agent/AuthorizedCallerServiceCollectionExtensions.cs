using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Agent;

/// <summary>
/// Registers a signed-in caller who holds every permission, for tool tests that exercise what a tool does once it is
/// allowed to run. The AI tools that change the site check the caller's own permission first (SEC-A3).
/// </summary>
internal static class AuthorizedCallerServiceCollectionExtensions
{
    public static IServiceCollection AddAuthorizedCaller(this IServiceCollection services)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin")], "Test"));

        return services
            .AddSingleton(authorization.Object)
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } });
    }
}

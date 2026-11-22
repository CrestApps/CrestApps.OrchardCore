using System.Reflection;
using System.Security.Claims;
using CrestApps.OrchardCore.AI.Agent;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Agent;

/// <summary>
/// Registers a signed-in caller who holds every permission, for tool tests that exercise what a tool does once it is
/// allowed to run. Every AI agent tool checks the caller's own permission first (SEC-A3), so each tool is given one here.
/// </summary>
internal static class AuthorizedCallerServiceCollectionExtensions
{
    private static readonly Permission _testPermission = new("TestAIToolPermission");

    public static IServiceCollection AddAuthorizedCaller(this IServiceCollection services)
        => services
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = SignedInUser() } })
            .AddAllowingAuthorization();

    /// <summary>
    /// Grants every permission to whoever is signed in, for tests that bring their own <see cref="IHttpContextAccessor"/>.
    /// </summary>
    public static IServiceCollection AddAllowingAuthorization(this IServiceCollection services)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        return services
            .AddSingleton(authorization.Object)
            .AddAgentToolPermissions();
    }

    /// <summary>
    /// Gives every AI agent tool a permission, as the agent's startups do, without registering anything else.
    /// </summary>
    public static IServiceCollection AddAgentToolPermissions(this IServiceCollection services)
        => services.Configure<AIToolPermissionOptions>(options =>
        {
            foreach (var name in AgentToolNames())
            {
                options.Require(name, _testPermission);
            }
        });

    public static ClaimsPrincipal SignedInUser()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin")], "Test"));

    public static IEnumerable<string> AgentToolNames()
        => typeof(Startup).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(AIFunction).IsAssignableFrom(type))
            .Select(type => type.GetField("TheName", BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue() as string)
            .Where(name => !string.IsNullOrEmpty(name));
}

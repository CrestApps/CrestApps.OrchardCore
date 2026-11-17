using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Json;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Agent;

/// <summary>
/// Builds what an agent tool test needs: the services of a tool call with a signed-in or anonymous caller, an
/// authorization service that follows the test's rule, a local clock in UTC, and helpers to call a tool and read its
/// result.
/// </summary>
internal static class AgentToolTestContext
{
    /// <summary>
    /// The user ID of the signed-in caller.
    /// </summary>
    public const string UserId = "user-1";

    /// <summary>
    /// The user name of the signed-in caller.
    /// </summary>
    public const string UserName = "admin";

    /// <summary>
    /// Creates the services of a tool call. Add the doubles of the services the tool uses before building the provider.
    /// </summary>
    /// <param name="signedIn">Whether a user is signed in.</param>
    /// <param name="authorize">The rule that grants or refuses a permission on a resource. The default grants everything.</param>
    /// <returns>The service collection.</returns>
    public static ServiceCollection CreateServices(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var httpContext = new DefaultHttpContext();

        if (signedIn)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, UserId),
                new Claim(ClaimTypes.Name, UserName),
            ], "Test"));
        }

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(accessor => accessor.HttpContext).Returns(httpContext);

        var services = new ServiceCollection();

        services.AddSingleton(httpContextAccessor.Object);
        services.AddSingleton(new FakeAuthorizationService(authorize ?? ((_, _) => true)));
        services.AddSingleton<IAuthorizationService>(provider => provider.GetRequiredService<FakeAuthorizationService>());
        services.AddSingleton(Options.Create(new DocumentJsonSerializerOptions()));
        services.AddSingleton(CreateLocalClock());

        return services;
    }

    /// <summary>
    /// Calls a tool and returns its result as text.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <param name="services">The services of the tool call.</param>
    /// <param name="arguments">The arguments of the tool call.</param>
    /// <returns>The result of the tool.</returns>
    public static async Task<string> InvokeAsync(AIFunction tool, IServiceProvider services, Dictionary<string, object> arguments = null)
    {
        var result = await tool.InvokeAsync(new AIFunctionArguments(arguments ?? [])
        {
            Services = services,
        }, TestContext.Current.CancellationToken);

        return result?.ToString();
    }

    /// <summary>
    /// Reads the JSON a tool returned.
    /// </summary>
    /// <param name="result">The result of the tool.</param>
    /// <returns>The root of the JSON.</returns>
    public static JsonElement ReadJson(string result)
    {
        using var document = JsonDocument.Parse(result);

        return document.RootElement.Clone();
    }

    private static ILocalClock CreateLocalClock()
    {
        var timeZone = new Mock<ITimeZone>();
        timeZone.Setup(zone => zone.TimeZoneId).Returns("UTC");

        var localClock = new Mock<ILocalClock>();
        localClock.Setup(clock => clock.GetLocalTimeZoneAsync()).ReturnsAsync(timeZone.Object);
        localClock.Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync((DateTimeOffset value) => value);
        localClock.Setup(clock => clock.ConvertToUtcAsync(It.IsAny<DateTime>())).ReturnsAsync((DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        return localClock.Object;
    }
}

using System.Globalization;
using System.Security.Claims;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Fulfillment.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Security.Permissions;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Fulfillment.AI;

/// <summary>
/// Calls a Fulfillment Partners tool on the <see cref="FulfillmentTestHarness"/>: its SQLite store, its real sender and
/// its partners. Each call runs in its own session, saved and closed at the end as a chat message's scope is, so the
/// harness can read the store afterwards. The request's user is always signed in, so a test of a caller who is not
/// signed in shows the tools read the caller from <see cref="IUserAccessor"/> only.
/// </summary>
internal static class FulfillmentToolServices
{
    /// <summary>
    /// Gets a signed-in caller.
    /// </summary>
    /// <returns>The caller.</returns>
    public static ClaimsPrincipal SignedIn()
        => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, AgentToolTestContext.UserId),
            new Claim(ClaimTypes.Name, AgentToolTestContext.UserName),
        ], "Test"));

    /// <summary>
    /// Gets a caller who is not signed in.
    /// </summary>
    /// <returns>The caller.</returns>
    public static ClaimsPrincipal Anonymous()
        => new(new ClaimsIdentity());

    /// <summary>
    /// Calls a tool in a session of its own.
    /// </summary>
    /// <param name="harness">The harness.</param>
    /// <param name="tool">The tool.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="caller">The caller the chat publishes through <see cref="IUserAccessor"/>; signed in by default.</param>
    /// <param name="authorize">The rule that grants or refuses a permission. The default grants everything.</param>
    /// <returns>The result as text, and the authorization service, to read the permissions asked.</returns>
    public static async Task<(string Result, FakeAuthorizationService Authorization)> InvokeAsync(
        FulfillmentTestHarness harness,
        AIFunction tool,
        Dictionary<string, object> arguments,
        ClaimsPrincipal caller = null,
        Func<Permission, object, bool> authorize = null)
    {
        await using var session = harness.Store.CreateSession();
        await using var services = Build(harness, session, caller ?? SignedIn(), authorize);

        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        await session.SaveChangesAsync(FulfillmentTestHarness.Token);

        return (result, services.GetRequiredService<FakeAuthorizationService>());
    }

    private static ServiceProvider Build(FulfillmentTestHarness harness, YesSqlSession session, ClaimsPrincipal caller, Func<Permission, object, bool> authorize)
    {
        var services = AgentToolTestContext.CreateServices(signedIn: true, authorize);
        var orders = new Mock<IOrderStore>();

        orders.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(id == harness.Order.ItemId ? harness.Order : null));
        orders.Setup(store => store.FindByNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string number, CancellationToken _) => Task.FromResult(number == harness.Order.OrderNumber ? harness.Order : null));

        services.AddSingleton(Mock.Of<IUserAccessor>(accessor => accessor.User == caller));
        services.AddSingleton(orders.Object);
        services.AddSingleton(new FulfillmentRequestStore(session));
        services.AddSingleton(harness.Sender(session));
        services.AddSingleton(new FulfillmentRequestText(new PassThroughLocalizer<FulfillmentRequestText>()));

        foreach (var provider in harness.Providers)
        {
            services.AddSingleton<IFulfillmentProvider>(provider);
        }

        return services.BuildServiceProvider();
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}

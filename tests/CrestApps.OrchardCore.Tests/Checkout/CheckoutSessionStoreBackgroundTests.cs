using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// A scheduled charge starts its checkout in a background task, whose stand-in request has no signed-in user, no remote
/// address and no browser. Starting the checkout there must work: the caller names the owner.
/// </summary>
public sealed class CheckoutSessionStoreBackgroundTests
{
    [Fact]
    public async Task NewAsync_InABackgroundTasksRequestWithoutARemoteAddress_StartsTheCheckoutForTheOwnerTheCallerNames()
    {
        // Arrange: Orchard Core gives a background task a request of its own, with no connection behind it.
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var addresses = new Mock<IClientIPAddressAccessor>();

        addresses.Setup(accessor => accessor.GetIPAddressAsync()).ReturnsAsync((System.Net.IPAddress)null);

        var store = new CheckoutSessionStore(
            accessor,
            addresses.Object,
            new GuestSessionTokenManager(accessor, new EphemeralDataProtectionProvider(), new StubClock(DateTime.UtcNow)),
            [],
            NullLogger<CheckoutSessionStore>.Instance,
            new StubClock(DateTime.UtcNow),
            Mock.Of<ISession>());

        // Act
        var session = await store.NewAsync("Transaction", "transaction-1", configure: session => session.OwnerId = "customer-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(("customer-1", (string)null), (session.OwnerId, session.IPAddress));
    }
}

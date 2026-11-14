using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.PayLater.Services;
using OrchardCore.Entities;
using Xunit;

namespace CrestApps.OrchardCore.Tests.PayLater;

/// <summary>
/// SEC-C5: Pay Later was offered to anyone whenever it was enabled. It now asks for a signed-in buyer by default and can
/// be limited to orders up to an amount.
/// </summary>
public sealed class PayLaterAvailabilityTests
{
    [Fact]
    public void IsAllowed_ForAGuest_IsFalseByDefault()
    {
        Assert.False(PayLaterCheckoutPaymentProvider.IsAllowed(new PayLaterSettings { Enabled = true }, Session(ownerId: null, total: 10m)));
    }

    [Fact]
    public void IsAllowed_ForASignedInBuyer_IsTrue()
    {
        Assert.True(PayLaterCheckoutPaymentProvider.IsAllowed(new PayLaterSettings { Enabled = true }, Session(ownerId: "user-1", total: 10m)));
    }

    [Fact]
    public void IsAllowed_ForAGuest_WhenTheStoreAllowsGuests_IsTrue()
    {
        Assert.True(PayLaterCheckoutPaymentProvider.IsAllowed(new PayLaterSettings { Enabled = true, RequireSignedIn = false }, Session(ownerId: null, total: 10m)));
    }

    [Fact]
    public void IsAllowed_OverTheLimit_IsFalse()
    {
        Assert.False(PayLaterCheckoutPaymentProvider.IsAllowed(new PayLaterSettings { Enabled = true, MaxOrderAmount = 50m }, Session(ownerId: "user-1", total: 60m)));
        Assert.True(PayLaterCheckoutPaymentProvider.IsAllowed(new PayLaterSettings { Enabled = true, MaxOrderAmount = 50m }, Session(ownerId: "user-1", total: 50m)));
    }

    [Fact]
    public void IsAllowed_WhenDisabled_IsFalse()
    {
        Assert.False(PayLaterCheckoutPaymentProvider.IsAllowed(new PayLaterSettings { Enabled = false, RequireSignedIn = false }, Session(ownerId: "user-1", total: 10m)));
    }

    private static CheckoutSession Session(string ownerId, decimal total)
    {
        var session = new CheckoutSession { SessionId = "s1", OwnerId = ownerId, Currency = "USD" };
        session.Put(new CheckoutInvoice { Currency = "USD", GrandTotal = total, DueNow = total, InitialPaymentAmount = total });

        return session;
    }
}

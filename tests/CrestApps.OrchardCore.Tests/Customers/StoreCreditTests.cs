using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// Store credit pays only for its own customer, signed in, in its currency; and refunds can be given as credit.
/// </summary>
public sealed class StoreCreditTests
{
    private static readonly DateTime _now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IStoreCreditStore> _store = new();
    private readonly Mock<ICustomerStore> _customers = new();

    public StoreCreditTests()
    {
        var account = new StoreCreditAccount { ItemId = "credit-1", CustomerId = "customer-1", Currency = "USD", Balance = 20m };

        _customers.Setup(value => value.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(new Customer { ItemId = "customer-1", UserId = "user-1" });
        _store.Setup(value => value.FindAsync("customer-1", "USD", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _store.Setup(value => value.FindByIdAsync("credit-1", It.IsAny<CancellationToken>())).ReturnsAsync(account);
    }

    [Fact]
    public async Task TheSignedInBuyersCredit_PaysTowardTheirCheckout()
    {
        var lines = await Provider().GetTendersAsync(Context("user-1", "USD", "credit-1"), TestContext.Current.CancellationToken);

        var line = Assert.Single(lines);
        Assert.Equal(20m, line.Amount);
        Assert.Equal("Store credit", line.Description);
    }

    [Fact]
    public async Task CreditDoesntPay_ForAGuest_AnotherBuyer_OrInAnotherCurrency()
    {
        Assert.Empty(await Provider().GetTendersAsync(Context(null, "USD", "credit-1"), TestContext.Current.CancellationToken));
        Assert.Empty(await Provider().GetTendersAsync(Context("user-2", "USD", "credit-1"), TestContext.Current.CancellationToken));
        Assert.Empty(await Provider().GetTendersAsync(Context("user-1", "EUR", "credit-1"), TestContext.Current.CancellationToken));
        Assert.Null(await Provider().GetBalanceAsync("credit-1", "EUR", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ARefund_CanBeGivenAsCredit_OnlyToABuyerWithAnAccount()
    {
        var provider = Provider();

        Assert.True(await provider.CanRefundAsync(new Order { UserId = "user-1" }, TestContext.Current.CancellationToken));
        Assert.False(await provider.CanRefundAsync(new Order { CustomerEmail = "guest@example.test" }, TestContext.Current.CancellationToken));
    }

    private StoreCreditTenderProvider Provider()
    {
        var service = new StoreCreditService(_store.Object, Mock.Of<ISession>(), Mock.Of<IDistributedLock>(), new TestClock(_now));

        return new StoreCreditTenderProvider(_store.Object, _customers.Object, service, new TestClock(_now), new PassThroughLocalizer<StoreCreditTenderProvider>());
    }

    private static CheckoutTenderContext Context(string ownerId, string currency, params string[] references)
        => new(new CheckoutInvoice { Currency = currency }, new CheckoutFlow(new CheckoutSession { SessionId = "session-1", OwnerId = ownerId, Currency = currency }), references);

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}

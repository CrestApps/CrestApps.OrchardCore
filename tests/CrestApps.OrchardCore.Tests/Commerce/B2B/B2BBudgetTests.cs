using CrestApps.OrchardCore.Accounts.B2B;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Commerce.B2B;

/// <summary>
/// SEC-C17s: an account's monthly budget counts what its buyers have committed, not only what's paid, and two of its
/// buyers paying at the same moment can't both spend what's left of it.
/// </summary>
public sealed class B2BBudgetTests
{
    private const string AccountId = "account-1";

    [Fact]
    public async Task TheBudget_CountsOrdersWaitingForApprovalOrApprovedButNotPaid()
    {
        // Arrange
        var harness = new Harness();

        harness.Add(new Order { ItemId = "paid", Status = OrderStatus.Paid, Subtotal = 40m, Total = 40m });
        harness.Add(new Order { ItemId = "waiting", Status = OrderStatus.AwaitingPayment, Subtotal = 30m, Approval = Approval(OrderApprovalStatus.Pending, 30m) });
        harness.Add(new Order { ItemId = "approved", Status = OrderStatus.Draft, Subtotal = 20m, Approval = Approval(OrderApprovalStatus.Approved, 20m) });

        // A checkout that was opened and left: nothing was committed, and the sweep cancels it.
        harness.Add(new Order { ItemId = "abandoned", Status = OrderStatus.AwaitingPayment, Subtotal = 500m });
        harness.Add(new Order { ItemId = "rejected", Status = OrderStatus.Canceled, Subtotal = 70m, Approval = Approval(OrderApprovalStatus.Rejected, 70m) });

        // Act
        var spent = await harness.Approvals.GetSpentThisMonthAsync(harness.Account, "USD", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(90m, spent);
    }

    [Fact]
    public async Task TheBudget_DoesntCountTheOrderBeingChecked()
    {
        // Arrange
        var harness = new Harness();

        harness.Add(new Order { ItemId = "waiting", Status = OrderStatus.AwaitingPayment, Subtotal = 30m, Approval = Approval(OrderApprovalStatus.Pending, 30m) });

        // Act
        var spent = await harness.Approvals.GetSpentThisMonthAsync(harness.Account, "USD", excludeOrderId: "waiting", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0m, spent);
    }

    [Fact]
    public async Task TwoBuyersPayingAtOnce_CantBothSpendWhatsLeftOfTheBudget()
    {
        // Arrange: 100 left this month, and two orders of 60, each fine on its own.
        var harness = new Harness(overlap: true);
        var first = harness.Checkout("order-a", "buyer-a", 60m);
        var second = harness.Checkout("order-b", "buyer-b", 60m);

        // Act: the first payment reads what's spent, then waits until the second one has arrived.
        var gates = await Task.WhenAll(harness.BeginPaymentAsync(first), harness.BeginPaymentAsync(second));

        // Assert
        Assert.Single(gates, gate => !gate.IsRefused);
        Assert.Contains("monthly budget", Assert.Single(gates, gate => gate.IsRefused).ErrorMessage);
    }

    [Fact]
    public async Task APaymentThatBegan_CountsAgainstTheBudget_UntilItsOrderIsPaidOrCanceled()
    {
        // Arrange
        var harness = new Harness();
        var first = harness.Checkout("order-a", "buyer-a", 60m);
        var second = harness.Checkout("order-b", "buyer-b", 60m);

        // Act
        var began = await harness.BeginPaymentAsync(first);
        var spent = await harness.Approvals.GetSpentThisMonthAsync(harness.Account, "USD", cancellationToken: TestContext.Current.CancellationToken);
        var refused = await harness.BeginPaymentAsync(second);

        harness.Order("order-a").Status = OrderStatus.Canceled;

        var afterCancel = await harness.BeginPaymentAsync(second);

        // Assert
        Assert.False(began.IsRefused);
        Assert.Equal(60m, spent);
        Assert.True(refused.IsRefused);
        Assert.False(afterCancel.IsRefused);
    }

    private static OrderApproval Approval(OrderApprovalStatus status, decimal amount)
        => new() { Status = status, Amount = amount, Currency = "USD" };

    /// <summary>
    /// An account with a budget of 100 and two buyers, whose orders are held in memory and read through the payment
    /// gate the checkout runs, with the site's lock.
    /// </summary>
    private sealed class Harness
    {
        private readonly List<Order> _orders = [];
        private readonly TaskCompletionSource _secondArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ServiceProvider _services;
        private readonly bool _overlap;
        private int _reads;
        private int _locks;

        public Harness(bool overlap = false)
        {
            _overlap = overlap;

            Account = TestB2BAccounts.Create(
                id: AccountId,
                configure: part =>
                {
                    part.Terms = new AccountTerms();
                    part.MonthlyBudget = 100m;
                },
                members:
                [
                    new AccountMember { UserId = "admin-1", Role = AccountMemberRole.Admin },
                    new AccountMember { UserId = "buyer-a", UserName = "ana", Role = AccountMemberRole.Member },
                    new AccountMember { UserId = "buyer-b", UserName = "ben", Role = AccountMemberRole.Member },
                ]);

            var accounts = new Mock<IB2BAccountStore>();
            accounts.Setup(store => store.FindByBuyerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Account);
            accounts.Setup(store => store.FindByIdAsync(AccountId, It.IsAny<CancellationToken>())).ReturnsAsync(Account);

            var orders = new Mock<IOrderStore>();
            orders.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string id, CancellationToken _) => ValueTask.FromResult(Order(id)));
            orders.Setup(store => store.PageAsync(It.IsAny<OrderQuery>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns((OrderQuery query, int _, int _, CancellationToken _) => PageAsync(query));

            var orderService = new Mock<IOrderService>();
            var site = new Mock<ISite>();
            site.Setup(value => value.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = "USD" });
            var siteService = new Mock<ISiteService>();
            siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

            _services = new ServiceCollection()
                .AddSingleton(orders.Object)
                .AddSingleton(orderService.Object)
                .AddSingleton(siteService.Object)
                .AddSingleton<ICommerceEmailSender>(CommerceEmailSenders.Create(new ServiceCollection().BuildServiceProvider()))
                .AddSingleton(Mock.Of<IClock>(clock => clock.UtcNow == DateTime.UtcNow))
                .AddSingleton(Mock.Of<IShopperCurrency>())
                .AddSingleton(Mock.Of<IHttpContextAccessor>())
                .AddSingleton(Mock.Of<LinkGenerator>())
                .AddSingleton(Mock.Of<global::YesSql.ISession>())
                .AddSingleton<IDistributedLock>(new SignalingLock(this))
                .AddSingleton(new B2BAccountService(accounts.Object, new HttpContextAccessor()))
                .AddSingleton<B2BApprovalService>()
                .AddSingleton<B2BCheckoutStepService>()
                .AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>))
                .BuildServiceProvider();
        }

        public B2BAccount Account { get; }

        public B2BApprovalService Approvals
            => _services.GetRequiredService<B2BApprovalService>();

        public void Add(Order order)
        {
            order.AccountId ??= AccountId;
            order.Currency ??= "USD";
            order.CreatedUtc = DateTime.UtcNow;

            lock (_orders)
            {
                _orders.Add(order);
            }
        }

        public Order Order(string id)
        {
            lock (_orders)
            {
                return _orders.FirstOrDefault(order => order.ItemId == id);
            }
        }

        public CheckoutSession Checkout(string orderId, string buyerId, decimal total)
        {
            Add(new Order { ItemId = orderId, UserId = buyerId, Status = OrderStatus.AwaitingPayment, Subtotal = total, CheckoutSessionId = "session-" + orderId });

            var session = new CheckoutSession
            {
                SessionId = "session-" + orderId,
                OwnerId = buyerId,
                ReferenceType = CheckoutReferenceTypes.Order,
                ReferenceId = orderId,
            };

            session.Put(new CheckoutInvoice { Currency = "USD", GrandTotal = total });

            return session;
        }

        public async Task<CheckoutPaymentBeginningContext> BeginPaymentAsync(CheckoutSession session)
        {
            // Each payment is its own request, with its own gate.
            var gate = ActivatorUtilities.CreateInstance<B2BApprovalPaymentGate>(_services);
            var context = new CheckoutPaymentBeginningContext(new CheckoutFlow(session), "account");

            await gate.BeginningAsync(context, TestContext.Current.CancellationToken);

            return context;
        }

        public void SignalLockRequested()
        {
            if (Interlocked.Increment(ref _locks) >= 2)
            {
                _secondArrived.TrySetResult();
            }
        }

        // The first read of what the account spent waits until a second payment has arrived, at the read or at the
        // account's lock, so two payments overlap.
        private async Task<(int Count, IReadOnlyList<Order> Orders)> PageAsync(OrderQuery query)
        {
            if (_overlap && Interlocked.Increment(ref _reads) == 1)
            {
                await _secondArrived.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
            else
            {
                _secondArrived.TrySetResult();
            }

            Order[] matches;

            lock (_orders)
            {
                matches = [.. _orders.Where(order =>
                    (query.AccountId is null || order.AccountId == query.AccountId) &&
                    (query.Statuses is not { Count: > 0 } || query.Statuses.Contains(order.Status)) &&
                    (query.CreatedOnOrAfter is null || order.CreatedUtc >= query.CreatedOnOrAfter))];
            }

            return (matches.Length, matches);
        }
    }

    /// <summary>
    /// The site's lock, telling the harness each time a key is asked for.
    /// </summary>
    private sealed class SignalingLock : IDistributedLock
    {
        private readonly LocalLock _inner = new(NullLogger<LocalLock>.Instance);
        private readonly Harness _harness;

        public SignalingLock(Harness harness)
            => _harness = harness;

        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
        {
            _harness.SignalLockRequested();

            return _inner.AcquireLockAsync(key, expiration);
        }

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
        {
            _harness.SignalLockRequested();

            return _inner.TryAcquireLockAsync(key, timeout, expiration);
        }

        public Task<bool> IsLockAcquiredAsync(string key)
            => _inner.IsLockAcquiredAsync(key);
    }
}

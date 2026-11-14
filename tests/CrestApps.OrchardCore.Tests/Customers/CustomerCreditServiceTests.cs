using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// A party's customer credit is the store credit of their customer record: money added to it is kept once per reference,
/// and money spent from it never takes more than there is.
/// </summary>
public sealed class CustomerCreditServiceTests
{
    private static readonly DateTime _now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private static readonly PartyRef _user = new(PartyKinds.User, "user-1");

    private readonly Dictionary<string, StoreCreditAccount> _accounts = new(StringComparer.Ordinal);
    private readonly Mock<ICustomerStore> _customers = new();
    private readonly Mock<ICustomerService> _customerService = new();

    public CustomerCreditServiceTests()
    {
        var customer = new Customer { ItemId = "customer-1", UserId = "user-1" };

        _customers.Setup(store => store.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        _customers.Setup(store => store.FindByIdAsync("customer-1", It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        _customerService.Setup(service => service.GetOrCreateForUserAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(customer);
    }

    [Fact]
    public async Task Add_KeepsTheCreditOnTheUsersCustomerRecord_OncePerReference()
    {
        // Arrange
        var service = CreateService();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var first = await service.AddAsync(Change(_user, 25m, "Transaction:tx-1:pay-1"), cancellationToken);
        var repeated = await service.AddAsync(Change(_user, 25m, "Transaction:tx-1:pay-1"), cancellationToken);

        // Assert
        Assert.True(first.Succeeded);
        Assert.False(first.AlreadyApplied);
        Assert.True(repeated.Succeeded);
        Assert.True(repeated.AlreadyApplied);
        Assert.Equal(25m, await service.GetBalanceAsync(_user, "USD", cancellationToken));
        Assert.Equal(25m, await service.GetBalanceAsync(new PartyRef(PartyKinds.Customer, "customer-1"), "usd", cancellationToken));

        var entry = Assert.Single(_accounts.Values.Single().Activity);
        Assert.Equal("Transaction:tx-1:pay-1", entry.Reference);
    }

    [Fact]
    public async Task Spend_NeverTakesMoreThanThereIs()
    {
        // Arrange
        var service = CreateService();
        var cancellationToken = TestContext.Current.CancellationToken;
        await service.AddAsync(Change(_user, 30m, "gift"), cancellationToken);

        // Act
        var tooMuch = await service.SpendAsync(Change(_user, 31m, "Transaction:tx-1:pay-2"), cancellationToken);
        var enough = await service.SpendAsync(Change(_user, 30m, "Transaction:tx-1:pay-3"), cancellationToken);

        // Assert
        Assert.False(tooMuch.Succeeded);
        Assert.Equal(CustomerCreditError.InsufficientBalance, tooMuch.Error);
        Assert.Equal(30m, tooMuch.Balance);
        Assert.True(enough.Succeeded);
        Assert.Equal(0m, enough.Balance);
    }

    [Fact]
    public async Task Spend_WithoutAnyCredit_IsRefused()
    {
        // Act
        var result = await CreateService().SpendAsync(Change(_user, 5m, "Transaction:tx-1:pay-1"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CustomerCreditError.InsufficientBalance, result.Error);
        Assert.Empty(_accounts);
    }

    [Fact]
    public async Task AGuest_CantHoldCredit()
    {
        // Arrange
        var service = CreateService();
        var guest = new PartyRef(PartyKinds.Guest, "guest-1");

        // Act
        var added = await service.AddAsync(Change(guest, 5m, "r"), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(service.CanHold(guest));
        Assert.Equal(CustomerCreditError.UnsupportedParty, added.Error);
        Assert.True(service.CanHold(_user));
    }

    [Fact]
    public async Task AnAccount_HoldsCreditOfItsOwn_KeyedByTheParty()
    {
        // Arrange
        var service = CreateService();
        var account = new PartyRef(PartyKinds.Account, "account-1");
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var added = await service.AddAsync(Change(account, 40m, "Transaction:tx-9:pay-1"), cancellationToken);
        var spent = await service.SpendAsync(Change(account, 15m, "Transaction:tx-9:pay-2"), cancellationToken);

        // Assert
        Assert.True(added.Succeeded);
        Assert.True(spent.Succeeded);
        Assert.True(service.CanHold(account));
        Assert.Equal(25m, await service.GetBalanceAsync(account, "USD", cancellationToken));
        Assert.Equal(0m, await service.GetBalanceAsync(_user, "USD", cancellationToken));

        var stored = Assert.Single(_accounts.Values);
        Assert.Equal((null, PartyKinds.Account, "account-1"), (stored.CustomerId, stored.PartyKind, stored.PartyId));
    }

    [Fact]
    public async Task OnlyAnAdminOfTheAccount_MaySpendItsCredit()
    {
        // Arrange: the same billing access rule as seeing what the account owes.
        var account = new PartyRef(PartyKinds.Account, "account-1");
        var membership = new Mock<IPartyMembershipService>();
        membership.Setup(service => service.IsMemberAsync(account, "admin-1", PartyAccess.Billing, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        membership.Setup(service => service.IsMemberAsync(account, "buyer-1", PartyAccess.Billing, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var service = CreateService(membership.Object);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act & Assert
        Assert.True(await service.CanSpendAsync(account, "admin-1", cancellationToken));
        Assert.False(await service.CanSpendAsync(account, "buyer-1", cancellationToken));
        Assert.True(await service.CanSpendAsync(_user, "user-1", cancellationToken));
        Assert.False(await service.CanSpendAsync(_user, "user-2", cancellationToken));
        Assert.False(await service.CanSpendAsync(new PartyRef(PartyKinds.Guest, "guest-1"), "user-1", cancellationToken));
    }

    private static CustomerCreditChange Change(PartyRef party, decimal amount, string reference)
        => new()
        {
            Party = party,
            Currency = "USD",
            Amount = amount,
            Reason = "Test",
            Reference = reference,
        };

    private StoreCreditCustomerCreditService CreateService(IPartyMembershipService membership = null)
    {
        var store = new Mock<IStoreCreditStore>();
        store
            .Setup(candidate => candidate.FindAsync(It.IsAny<StoreCreditHolder>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StoreCreditHolder holder, string currency, CancellationToken _) => _accounts.Values.FirstOrDefault(account =>
                (string.IsNullOrEmpty(holder.CustomerId)
                    ? account.PartyKind == holder.Party.Kind && account.PartyId == holder.Party.Id
                    : account.CustomerId == holder.CustomerId) &&
                string.Equals(account.Currency, currency?.Trim(), StringComparison.OrdinalIgnoreCase)));
        store
            .Setup(candidate => candidate.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => _accounts.GetValueOrDefault(id));
        store
            .Setup(candidate => candidate.CreateAsync(It.IsAny<StoreCreditAccount>(), It.IsAny<CancellationToken>()))
            .Callback((StoreCreditAccount account, CancellationToken _) => _accounts[account.ItemId] = account)
            .Returns(ValueTask.CompletedTask);

        var storeCredit = new StoreCreditService(store.Object, Mock.Of<ISession>(), new LocalLock(NullLogger<LocalLock>.Instance), new TestClock(_now));
        var services = new ServiceCollection();

        if (membership is not null)
        {
            services.AddSingleton(membership);
        }

        return new StoreCreditCustomerCreditService(storeCredit, store.Object, _customers.Object, _customerService.Object, services.BuildServiceProvider());
    }
}

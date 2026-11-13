using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
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

    [Theory]
    [InlineData(PartyKinds.Account)]
    [InlineData(PartyKinds.Guest)]
    [InlineData(PartyKinds.Contact)]
    public async Task OnlyAUserOrACustomerRecord_CanHoldCredit(string kind)
    {
        // Arrange
        var service = CreateService();
        var party = new PartyRef(kind, "party-1");

        // Act
        var added = await service.AddAsync(Change(party, 5m, "r"), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(service.CanHold(party));
        Assert.Equal(CustomerCreditError.UnsupportedParty, added.Error);
        Assert.True(service.CanHold(_user));
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

    private StoreCreditCustomerCreditService CreateService()
    {
        var store = new Mock<IStoreCreditStore>();
        store
            .Setup(candidate => candidate.FindAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string customerId, string currency, CancellationToken _) => _accounts.Values.FirstOrDefault(account =>
                account.CustomerId == customerId &&
                string.Equals(account.Currency, currency?.Trim(), StringComparison.OrdinalIgnoreCase)));
        store
            .Setup(candidate => candidate.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => _accounts.GetValueOrDefault(id));
        store
            .Setup(candidate => candidate.CreateAsync(It.IsAny<StoreCreditAccount>(), It.IsAny<CancellationToken>()))
            .Callback((StoreCreditAccount account, CancellationToken _) => _accounts[account.ItemId] = account)
            .Returns(ValueTask.CompletedTask);

        var storeCredit = new StoreCreditService(store.Object, Mock.Of<ISession>(), new LocalLock(NullLogger<LocalLock>.Instance), new TestClock(_now));

        return new StoreCreditCustomerCreditService(storeCredit, store.Object, _customers.Object, _customerService.Object);
    }
}

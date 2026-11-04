using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Deployments;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Recipes;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// Store credit, loyalty points and referral codes travel between sites: each is exported, imported into an empty store
/// with the same ids and content, matched on a second run instead of duplicated, and the database document id of the
/// exporting site never travels.
/// </summary>
public sealed class CustomerRecordsPortabilityTests
{
    private static readonly DateTime _created = new(2026, 10, 1, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task StoreCredit_ExportsAndImportsIntoAnEmptyStore()
    {
        // Arrange
        var source = new InMemoryStoreCreditStore();
        var account = new StoreCreditAccount { Id = 41, ItemId = "4cr00000000000000000000001", CustomerId = "customer-1", Currency = "USD", Balance = 25m, CreatedUtc = _created };

        account.ChangeBalance(25m, "Goodwill", _created);
        account.Balance = 25m;
        source.Entries.Add(account);

        var target = new InMemoryStoreCreditStore();
        var step = new StoreCreditAccountsStep(target, RecipeRoundTrip.Localizer<StoreCreditAccountsStep>());

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new StoreCreditAccountsDeploymentSource(source), new StoreCreditAccountsDeploymentStep());
        var first = await RecipeRoundTrip.RunAsync(step, exported);
        var second = await RecipeRoundTrip.RunAsync(step, exported);

        // Assert
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Equal("StoreCreditAccounts", exported["name"].GetValue<string>());
        Assert.False(exported["Accounts"][0].AsObject().ContainsKey("Id"));

        var imported = Assert.Single(target.Entries);

        Assert.Equal(account.ItemId, imported.ItemId);
        Assert.Equal(0, imported.Id);
        Assert.Equal("customer-1", imported.CustomerId);
        Assert.Equal("USD", imported.Currency);
        Assert.Equal(25m, imported.Balance);
        Assert.Equal(_created, imported.CreatedUtc);
        Assert.Equal("Goodwill", Assert.Single(imported.Activity).Reason);
        Assert.Equal(1, target.UpdateCount);
    }

    [Fact]
    public async Task StoreCredit_MatchesAnAccountWithoutAnIdByCustomerAndCurrency()
    {
        // Arrange
        var target = new InMemoryStoreCreditStore();

        target.Entries.Add(new StoreCreditAccount { ItemId = "existing", CustomerId = "customer-1", Currency = "USD", Balance = 5m });

        var step = new StoreCreditAccountsStep(target, RecipeRoundTrip.Localizer<StoreCreditAccountsStep>());
        var recipe = JsonNode.Parse("""{ "name": "StoreCreditAccounts", "Accounts": [ { "CustomerId": "customer-1", "Currency": "usd", "Balance": 30 } ] }""").AsObject();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Empty(errors);
        Assert.Equal(30m, Assert.Single(target.Entries).Balance);
    }

    [Fact]
    public async Task StoreCredit_RefusesAnAccountWithoutACustomer()
    {
        // Arrange
        var target = new InMemoryStoreCreditStore();
        var step = new StoreCreditAccountsStep(target, RecipeRoundTrip.Localizer<StoreCreditAccountsStep>());
        var recipe = JsonNode.Parse("""{ "name": "StoreCreditAccounts", "Accounts": [ { "Currency": "USD", "Balance": 30 } ] }""").AsObject();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Equal(["A store credit account needs a customer."], errors);
        Assert.Empty(target.Entries);
    }

    [Fact]
    public async Task Loyalty_ExportsAndImportsIntoAnEmptyStore()
    {
        // Arrange
        var source = new InMemoryLoyaltyStore();
        var account = new LoyaltyAccount { Id = 7, ItemId = "4ya00000000000000000000001", CustomerId = "customer-1", CreatedUtc = _created };

        account.ChangePoints(1200, LoyaltyActivityKind.Earned, "Order 1001", _created, orderId: "order-1");
        source.Entries.Add(account);

        var target = new InMemoryLoyaltyStore();
        var step = new LoyaltyAccountsStep(target, RecipeRoundTrip.Localizer<LoyaltyAccountsStep>());

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new LoyaltyAccountsDeploymentSource(source), new LoyaltyAccountsDeploymentStep());
        var first = await RecipeRoundTrip.RunAsync(step, exported);
        var second = await RecipeRoundTrip.RunAsync(step, exported);

        // Assert
        Assert.Empty(first);
        Assert.Empty(second);

        var imported = Assert.Single(target.Entries);

        Assert.Equal(account.ItemId, imported.ItemId);
        Assert.Equal(1200, imported.Points);
        Assert.Equal(1200, imported.GetEarnedFor("order-1"));
    }

    [Fact]
    public async Task Referrals_ExportAndImportIntoAnEmptyStore_AndRefuseACodeAnotherCustomerUses()
    {
        // Arrange
        var source = new InMemoryReferralStore();

        source.Entries.Add(new ReferralProfile
        {
            ItemId = "4rf00000000000000000000001",
            CustomerId = "customer-1",
            Code = "ANA10",
            Referrals = [new ReferralRecord { FriendName = "Sam", OrderNumber = "1002", RewardKind = ReferralRewardKind.Points, RewardPoints = 500 }],
        });

        var target = new InMemoryReferralStore();
        var step = new ReferralProfilesStep(target, RecipeRoundTrip.Localizer<ReferralProfilesStep>());
        var clash = JsonNode.Parse("""{ "name": "ReferralProfiles", "Profiles": [ { "CustomerId": "customer-2", "Code": "ANA10" } ] }""").AsObject();

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new ReferralProfilesDeploymentSource(source), new ReferralProfilesDeploymentStep());
        var first = await RecipeRoundTrip.RunAsync(step, exported);
        var second = await RecipeRoundTrip.RunAsync(step, exported);
        var refused = await RecipeRoundTrip.RunAsync(step, clash);

        // Assert
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Equal(["The referral code 'ANA10' is already used by another customer."], refused);

        var imported = Assert.Single(target.Entries);

        Assert.Equal("ANA10", imported.Code);
        Assert.Equal(500, Assert.Single(imported.Referrals).RewardPoints);
    }

    [Fact]
    public async Task Customers_ExportGuestsAndBusinesses_InTheShapeTheCustomersStepReads()
    {
        // Arrange
        var store = new Mock<ICustomerStore>();
        var guest = new Customer
        {
            ItemId = "4cs00000000000000000000001",
            Kind = CustomerKind.Business,
            CompanyName = "Contoso",
            Email = "buyer@contoso.test",
            Addresses = [new CustomerAddress { AddressId = "office", Label = "Office" }],
            DefaultBillingAddressId = "office",
            PrivacyLog = [new CustomerPrivacyEntry()],
        };
        var merged = new Customer { ItemId = "merged", Email = "old@contoso.test", Status = CustomerStatus.Merged };

        store.Setup(value => value.PageAsync(It.Is<CustomerQuery>(query => query.HasAccount == false && query.NotAnonymized), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((2, (IReadOnlyList<Customer>)[guest, merged]));

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new CustomersDeploymentSource(store.Object), new CustomersDeploymentStep());

        // Assert
        Assert.Equal("Customers", exported["name"].GetValue<string>());

        var customer = Assert.Single(exported["Customers"].AsArray()).AsObject();

        Assert.Equal(guest.ItemId, customer["ItemId"].GetValue<string>());
        Assert.Equal("Business", customer["Kind"].GetValue<string>());
        Assert.Equal("Contoso", customer["CompanyName"].GetValue<string>());
        Assert.Equal("office", customer["DefaultBillingAddressId"].GetValue<string>());
        Assert.Equal("office", customer["Addresses"][0]["AddressId"].GetValue<string>());
        Assert.False(customer.ContainsKey("PrivacyLog"));
        Assert.False(customer.ContainsKey("Id"));
    }

    private sealed class InMemoryStoreCreditStore : InMemoryCatalog<StoreCreditAccount>, IStoreCreditStore
    {
        public Task<StoreCreditAccount> FindAsync(string customerId, string currency, CancellationToken cancellationToken = default)
            => Task.FromResult(Entries.FirstOrDefault(entry => entry.CustomerId == customerId && entry.Currency == currency));

        public Task<IReadOnlyList<StoreCreditAccount>> ListByCustomerAsync(string customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StoreCreditAccount>>(Entries.Where(entry => entry.CustomerId == customerId).ToList());
    }

    private sealed class InMemoryLoyaltyStore : InMemoryCatalog<LoyaltyAccount>, ILoyaltyStore
    {
        public Task<LoyaltyAccount> FindByCustomerAsync(string customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(Entries.FirstOrDefault(entry => entry.CustomerId == customerId));
    }

    private sealed class InMemoryReferralStore : InMemoryCatalog<ReferralProfile>, IReferralStore
    {
        public Task<ReferralProfile> FindByCustomerAsync(string customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(Entries.FirstOrDefault(entry => entry.CustomerId == customerId));

        public Task<ReferralProfile> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult(Entries.FirstOrDefault(entry => string.Equals(entry.Code, code, StringComparison.OrdinalIgnoreCase)));
    }
}

using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Deployments;
using CrestApps.OrchardCore.Inventory.Recipes;
using CrestApps.OrchardCore.Marketplace.Deployments;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Recipes;
using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Markets.Deployments;
using CrestApps.OrchardCore.Markets.Models;
using CrestApps.OrchardCore.Markets.Recipes;
using CrestApps.OrchardCore.Markets.Services;
using CrestApps.OrchardCore.PointOfSale.Deployments;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Recipes;
using CrestApps.OrchardCore.PointOfSale.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Moq;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// Markets, registers, stock locations and vendors travel between sites: each is exported, imported into an empty
/// store with the same ids and content, and matched on a second run instead of duplicated. What belongs to the
/// exporting site, such as a vendor's payout account, never travels.
/// </summary>
public sealed class CommerceSetupPortabilityTests
{
    [Fact]
    public async Task Markets_ExportAndImportIntoAnEmptyStore_WithoutDuplicates()
    {
        // Arrange
        var source = new InMemoryMarketStore();

        source.Entries.Add(new Market { ItemId = "4mk00000000000000000000001", Name = "Europe", Countries = ["DE", "AT"], Currency = "EUR", ExchangeRate = 0.92m, Rounding = MarketPriceRounding.EndIn99 });

        var target = new InMemoryMarketStore();
        var step = new MarketsStep(target, RecipeRoundTrip.Localizer<MarketsStep>());

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new MarketsDeploymentSource(source), new MarketsDeploymentStep());
        var first = await RecipeRoundTrip.RunAsync(step, exported);
        var second = await RecipeRoundTrip.RunAsync(step, exported);

        // Assert
        Assert.Equal("Markets", exported["name"].GetValue<string>());
        Assert.Empty(first);
        Assert.Empty(second);

        var market = Assert.Single(target.Entries);

        Assert.Equal("4mk00000000000000000000001", market.ItemId);
        Assert.Equal(["DE", "AT"], market.Countries);
        Assert.Equal("EUR", market.Currency);
        Assert.Equal(0.92m, market.ExchangeRate);
        Assert.Equal(MarketPriceRounding.EndIn99, market.Rounding);
    }

    [Fact]
    public async Task Markets_RefuseACountryAnotherMarketHas()
    {
        // Arrange
        var target = new InMemoryMarketStore();

        target.Entries.Add(new Market { ItemId = "4mk00000000000000000000001", Name = "Europe", Countries = ["DE"], Currency = "EUR" });

        var step = new MarketsStep(target, RecipeRoundTrip.Localizer<MarketsStep>());
        var recipe = JsonNode.Parse("""{ "name": "Markets", "Markets": [ { "Name": "Germany", "Countries": [ "de" ], "Currency": "EUR", "ExchangeRate": 1 } ] }""").AsObject();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Equal(["DE is already in the market Europe."], errors);
        Assert.Single(target.Entries);
    }

    [Fact]
    public async Task Registers_ExportAndImportIntoAnEmptyStore_WithoutDuplicates()
    {
        // Arrange
        var source = new InMemoryRegisterStore();

        source.Entries.Add(new Register
        {
            Id = 12,
            ItemId = "4rg00000000000000000000001",
            Name = "Front till",
            LocationId = "default",
            Address = new Address { AddressLine1 = "1 Main St", City = "Springfield", Country = "US" },
            Currency = "USD",
            Roles = ["Cashier"],
        });

        var target = new InMemoryRegisterStore();
        var locations = new Mock<IInventoryLocationService>();

        locations.Setup(value => value.GetLocationsAsync()).ReturnsAsync([new InventoryLocation { LocationId = "default", Name = "Main location" }]);

        var step = new RegistersStep(target, locations.Object, RecipeRoundTrip.Localizer<RegistersStep>());

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new RegistersDeploymentSource(source), new RegistersDeploymentStep());
        var first = await RecipeRoundTrip.RunAsync(step, exported);
        var second = await RecipeRoundTrip.RunAsync(step, exported);

        // Assert
        Assert.Equal("Registers", exported["name"].GetValue<string>());
        Assert.False(exported["Registers"][0].AsObject().ContainsKey("Id"));
        Assert.Empty(first);
        Assert.Empty(second);

        var register = Assert.Single(target.Entries);

        Assert.Equal("4rg00000000000000000000001", register.ItemId);
        Assert.Equal(0, register.Id);
        Assert.Equal("Springfield", register.Address.City);
        Assert.Equal(["Cashier"], register.Roles);
    }

    [Fact]
    public async Task Registers_RefuseALocationThatDoesNotExist()
    {
        // Arrange
        var target = new InMemoryRegisterStore();
        var locations = new Mock<IInventoryLocationService>();

        locations.Setup(value => value.GetLocationsAsync()).ReturnsAsync([new InventoryLocation { LocationId = "default" }]);

        var step = new RegistersStep(target, locations.Object, RecipeRoundTrip.Localizer<RegistersStep>());
        var recipe = JsonNode.Parse("""
            { "name": "Registers", "Registers": [ { "Name": "Till", "LocationId": "warehouse", "Address": { "AddressLine1": "1 Main St", "City": "Springfield", "Country": "US" } } ] }
            """).AsObject();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Equal(["Choose the store location the register sells from."], errors);
        Assert.Empty(target.Entries);
    }

    [Fact]
    public async Task StockLocations_KeepTheirIds_AndLeaveOutLocationsAnotherModuleOwns()
    {
        // Arrange
        var source = new InMemoryCatalog<StockLocation>();

        source.Entries.Add(new StockLocation { ItemId = "default", Name = "Main warehouse", Address = "1 Dock Rd", Position = 1 });
        source.Entries.Add(new StockLocation { ItemId = "4sl00000000000000000000001", Name = "Van 7", OwnerKind = "Asset", OwnerId = "asset-7" });

        var target = new InMemoryCatalog<StockLocation>();
        var step = new StockLocationsStep(target, RecipeRoundTrip.Localizer<StockLocationsStep>());

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new StockLocationsDeploymentSource(source), new StockLocationsDeploymentStep());
        var first = await RecipeRoundTrip.RunAsync(step, exported);
        var second = await RecipeRoundTrip.RunAsync(step, exported);

        // Assert
        Assert.Equal("StockLocations", exported["name"].GetValue<string>());
        Assert.Single(exported["Locations"].AsArray());
        Assert.Empty(first);
        Assert.Empty(second);

        var location = Assert.Single(target.Entries);

        Assert.Equal("default", location.ItemId);
        Assert.Equal("Main warehouse", location.Name);
        Assert.Equal("1 Dock Rd", location.Address);
    }

    [Fact]
    public async Task Vendors_TravelWithTheirUsersByName_AndNeverWithTheirPayoutAccount()
    {
        // Arrange
        var source = new InMemoryVendorStore();

        source.Entries.Add(new Vendor
        {
            ItemId = "4vd00000000000000000000001",
            Name = "Acme Crafts",
            UserIds = ["source-user-id"],
            UserNames = ["acme"],
            CommissionPercent = 12.5m,
            StripeAccountId = "acct_123",
            StripeAccountRequestKey = "request-key",
            PayoutsEnabled = true,
        });

        var target = new InMemoryVendorStore();
        var users = new Mock<IUserService>();

        users.Setup(value => value.GetUserAsync("acme")).ReturnsAsync(new User { UserId = "target-user-id", UserName = "acme" });

        var step = new VendorsStep(target, users.Object, RecipeRoundTrip.Localizer<VendorsStep>());

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new VendorsDeploymentSource(source), new VendorsDeploymentStep());
        var first = await RecipeRoundTrip.RunAsync(step, exported);
        var second = await RecipeRoundTrip.RunAsync(step, exported);

        // Assert
        var json = exported.ToJsonString();

        Assert.Equal("Vendors", exported["name"].GetValue<string>());
        Assert.DoesNotContain("acct_123", json);
        Assert.DoesNotContain("request-key", json);
        Assert.DoesNotContain("source-user-id", json);
        Assert.Empty(first);
        Assert.Empty(second);

        var vendor = Assert.Single(target.Entries);

        Assert.Equal("4vd00000000000000000000001", vendor.ItemId);
        Assert.Equal(["target-user-id"], vendor.UserIds);
        Assert.Equal(12.5m, vendor.CommissionPercent);
        Assert.Null(vendor.StripeAccountId);
        Assert.False(vendor.PayoutsEnabled);
    }

    [Fact]
    public async Task Vendors_ReportAUserThatDoesNotExist()
    {
        // Arrange
        var target = new InMemoryVendorStore();
        var step = new VendorsStep(target, Mock.Of<IUserService>(), RecipeRoundTrip.Localizer<VendorsStep>());
        var recipe = JsonNode.Parse("""{ "name": "Vendors", "Vendors": [ { "Name": "Acme", "UserNames": [ "nobody" ] } ] }""").AsObject();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Equal(["There's no user named nobody."], errors);
        Assert.Empty(target.Entries);
    }

    private sealed class InMemoryMarketStore : InMemoryCatalog<Market>, IMarketStore
    {
        public Task<(int Count, IReadOnlyList<Market> Markets)> PageAsync(int skip, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<(int, IReadOnlyList<Market>)>((Entries.Count, Entries.Skip(skip).Take(take).ToList()));
    }

    private sealed class InMemoryRegisterStore : InMemoryCatalog<Register>, IRegisterStore
    {
        public Task<(int Count, IReadOnlyList<Register> Registers)> PageAsync(int skip, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<(int, IReadOnlyList<Register>)>((Entries.Count, Entries.Skip(skip).Take(take).ToList()));
    }

    private sealed class InMemoryVendorStore : InMemoryCatalog<Vendor>, IVendorStore
    {
        public Task<IReadOnlyList<Vendor>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Vendor>>(Entries.Where(entry => entry.UserIds.Contains(userId)).ToList());

        public Task<IReadOnlyList<Vendor>> ListAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Vendor>>(Entries.ToList());

        public Task<(int Count, IReadOnlyList<Vendor> Vendors)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<(int, IReadOnlyList<Vendor>)>((Entries.Count, Entries.Skip(skip).Take(take).ToList()));
    }
}

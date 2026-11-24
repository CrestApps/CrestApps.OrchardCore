using System.Text.Json;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Marketplace;
using CrestApps.OrchardCore.Marketplace.AI;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Security.Permissions;
using MarketplaceAIToolsStartup = CrestApps.OrchardCore.Marketplace.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Marketplace.AI;

/// <summary>
/// <c>getVendor</c> needs Manage the marketplace, totals what the vendor's page totals and never returns the payout
/// account's id; vendors are a read-only kind of the record tools, listed through the vendors page's query.
/// </summary>
public sealed class MarketplaceAIToolTests
{
    private readonly Vendor _vendor = new()
    {
        ItemId = "vendor-1",
        Name = "Clay & Co",
        Email = "clay@example.com",
        UserNames = ["clayco"],
        UserIds = ["user-9"],
        CommissionPercent = 12m,
        Country = "US",
        StripeAccountId = "acct_SECRET_ID",
        StripeAccountRequestKey = "REQUEST_KEY_SECRET",
        PayoutsEnabled = true,
    };

    private readonly Mock<IVendorStore> _vendors = new();
    private readonly Mock<IVendorEarningStore> _earnings = new();
    private readonly Mock<IVendorPayoutStore> _payouts = new();

    public MarketplaceAIToolTests()
    {
        _vendors.Setup(store => store.FindByIdAsync("vendor-1", It.IsAny<CancellationToken>())).ReturnsAsync(_vendor);
        _vendors.Setup(store => store.PageAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((1, (IReadOnlyList<Vendor>)[_vendor]));
        _earnings.Setup(store => store.ListByVendorAsync("vendor-1", 500, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new VendorEarning { ItemId = "e1", VendorId = "vendor-1", OrderId = "o1", OrderNumber = "1001", Currency = "USD", Net = 40m, Status = VendorEarningStatus.Held },
            new VendorEarning { ItemId = "e2", VendorId = "vendor-1", OrderId = "o2", OrderNumber = "1002", Currency = "USD", Net = 60m, Status = VendorEarningStatus.Available },
            new VendorEarning { ItemId = "e3", VendorId = "vendor-1", OrderId = "o3", OrderNumber = "1003", Currency = "USD", Net = 25m, Status = VendorEarningStatus.PaidOut, PayoutId = "p1" },
        ]);
        _payouts.Setup(store => store.ListByVendorAsync("vendor-1", 100, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new VendorPayout { ItemId = "p1", VendorId = "vendor-1", OrderId = "o3", Amount = 25m, Currency = "USD", Status = VendorPayoutStatus.Failed, Failure = "Account restricted", TransferId = "tr_SECRET" },
        ]);
    }

    [Fact]
    public async Task GetVendor_RefusesACallerWhoIsNotSignedIn()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetVendorTool(), Build(signedIn: false), new() { ["vendorId"] = "vendor-1" });

        // Assert
        Assert.Equal(MarketplaceAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_vendors.Invocations);
    }

    [Fact]
    public async Task GetVendor_RefusesACallerWithoutManageMarketplace()
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != MarketplacePermissions.ManageMarketplace.Name);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetVendorTool(), services, new() { ["vendorId"] = "vendor-1" });

        // Assert
        Assert.Equal(MarketplaceAIToolBase.NotAllowedMessage, result);
        Assert.Equal(MarketplacePermissions.ManageMarketplace.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_vendors.Invocations);
    }

    [Fact]
    public async Task GetVendor_ReturnsTheTotalsPerCurrency_AndNeverThePayoutAccountId()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetVendorTool(), Build(), new() { ["vendorId"] = "vendor-1" });
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        var total = Assert.Single(json.GetProperty("totals").EnumerateArray());
        Assert.Equal(40m, total.GetProperty("held").GetDecimal());
        Assert.Equal(60m, total.GetProperty("available").GetDecimal());
        Assert.Equal(25m, total.GetProperty("paidOut").GetDecimal());
        Assert.True(json.GetProperty("vendor").GetProperty("hasPayoutAccount").GetBoolean());
        Assert.Equal("Account restricted", Assert.Single(json.GetProperty("payouts").EnumerateArray()).GetProperty("failure").GetString());
        Assert.DoesNotContain("acct_SECRET_ID", result, StringComparison.Ordinal);
        Assert.DoesNotContain("REQUEST_KEY_SECRET", result, StringComparison.Ordinal);
        Assert.DoesNotContain("tr_SECRET", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetVendor_SaysWhenTheVendorIsNotFound()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetVendorTool(), Build(), new() { ["vendorId"] = "missing" });

        // Assert
        Assert.Contains("No vendor has the id 'missing'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheVendorKind_IsReadOnly_ListsThroughTheVendorsQuery_AndProjectsNoSecret()
    {
        // Arrange
        var registrations = new ServiceCollection();
        new MarketplaceAIToolsStartup(new PassThroughStringLocalizer<MarketplaceAIToolsStartup>()).ConfigureServices(registrations);

        using var options = registrations.BuildServiceProvider();
        var kind = Assert.IsType<AIRecordKind<Vendor>>(options.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds["vendor"]);
        var services = Build();

        // Act
        var page = await kind.ListAsync(services, new AIRecordQuery { Text = "Clay", Page = 1, PageSize = 20 }, TestContext.Current.CancellationToken);
        var found = await kind.FindAsync(services, "vendor-1", TestContext.Current.CancellationToken);
        var projected = JsonSerializer.Serialize(kind.Projection(found));

        // Assert
        Assert.False(kind.CanWrite);
        Assert.Equal(MarketplacePermissions.ManageMarketplace.Name, kind.Permission.Name);
        Assert.Equal(1, page.Count);
        Assert.Same(_vendor, Assert.Single(page.Entries));
        Assert.DoesNotContain("acct_SECRET_ID", projected, StringComparison.Ordinal);
        Assert.DoesNotContain("REQUEST_KEY_SECRET", projected, StringComparison.Ordinal);
        _vendors.Verify(store => store.PageAsync("Clay", 0, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(_vendors.Object);
        services.AddSingleton(_earnings.Object);
        services.AddSingleton(_payouts.Object);

        return services.BuildServiceProvider();
    }
}

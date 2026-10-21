using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Commerce.Services;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// The setup guide's staff roles cover each job without more than it needs, and its shipping step ships everywhere at
/// the rate entered, once.
/// </summary>
public sealed class StoreSetupTests
{
    [Fact]
    public void EveryStaffRole_OpensTheAdmin_AndHasAName_OfItsOwn()
    {
        Assert.Equal(StaffRolePresets.All.Count, StaffRolePresets.All.Select(preset => preset.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(StaffRolePresets.All, preset => Assert.Contains("AccessAdminPanel", preset.Permissions));
        Assert.All(StaffRolePresets.All, preset => Assert.Equal(preset.Permissions.Count, preset.Permissions.Distinct(StringComparer.Ordinal).Count()));
    }

    [Fact]
    public void TheWarehouse_ShipsAndCountsStock_ButDoesntRefund_OrEditProducts()
    {
        var warehouse = StaffRolePresets.Warehouse.Permissions;

        Assert.Contains("FulfillOrders", warehouse);
        Assert.Contains("ManageInventory", warehouse);
        Assert.DoesNotContain("ManageRefunds", warehouse);
        Assert.DoesNotContain("ManageOrders", warehouse);
        Assert.DoesNotContain("EditContent", warehouse);

        // Marketing edits products and promotions, not orders or money.
        Assert.Contains("ManageCoupons", StaffRolePresets.Marketing.Permissions);
        Assert.DoesNotContain("ManageRefunds", StaffRolePresets.Marketing.Permissions);
        Assert.DoesNotContain("ManageOrders", StaffRolePresets.Marketing.Permissions);

        // The store manager does what the warehouse and support do.
        Assert.All(StaffRolePresets.Warehouse.Permissions.Concat(StaffRolePresets.CustomerSupport.Permissions), permission => Assert.Contains(permission, StaffRolePresets.StoreManager.Permissions));
    }

    [Fact]
    public async Task Shipping_IsSetUp_ByOneZone_ForEverywhere_AtTheRateEntered()
    {
        var zones = new List<ShippingZone>();
        var step = CreateShippingStep(zones);

        Assert.False((await step.DescribeAsync(TestContext.Current.CancellationToken)).IsDone);

        var refused = await step.RunAsync(Values("amount", "five"), TestContext.Current.CancellationToken);

        Assert.False(refused.Succeeded);
        Assert.Empty(zones);

        var result = await step.RunAsync(Values("amount", "7.5"), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);

        var zone = Assert.Single(zones);
        var method = Assert.Single(zone.Methods);

        Assert.True(zone.RestOfWorld);
        Assert.Equal(ShippingConstants.Providers.Flat, method.ProviderName);
        Assert.Equal("EUR", method.Currency);
        Assert.True(method.TryGet<FlatRateSettings>(out var flat));
        Assert.Equal(7.5m, flat.Amount);
        Assert.True((await step.DescribeAsync(TestContext.Current.CancellationToken)).IsDone);

        // A second run doesn't add another zone of the same name.
        Assert.False((await step.RunAsync(Values("amount", "5"), TestContext.Current.CancellationToken)).Succeeded);
        Assert.Single(zones);
    }

    private static StoreSetupActionContext Values(string name, string value)
        => new() { Values = new Dictionary<string, string> { [name] = value } };

    private static ShippingSetupStep CreateShippingStep(List<ShippingZone> zones)
    {
        var catalog = new Mock<INamedCatalog<ShippingZone>>();
        catalog.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => [.. zones]);
        catalog.Setup(value => value.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) => zones.FirstOrDefault(zone => zone.Name == name));

        var manager = new Mock<INamedCatalogManager<ShippingZone>>();
        manager.Setup(value => value.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => new ShippingZone { ItemId = "zone" });
        manager.Setup(value => value.CreateAsync(It.IsAny<ShippingZone>(), It.IsAny<CancellationToken>()))
            .Callback((ShippingZone zone, CancellationToken _) => zones.Add(zone))
            .Returns(ValueTask.CompletedTask);

        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = "EUR" });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new ShippingSetupStep(catalog.Object, manager.Object, siteService.Object, Mock.Of<LinkGenerator>(), new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, new PassThroughStringLocalizer<ShippingSetupStep>());
    }
}

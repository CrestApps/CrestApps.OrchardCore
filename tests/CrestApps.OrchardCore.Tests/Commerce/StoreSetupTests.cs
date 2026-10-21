using CrestApps.Core.Services;
using CrestApps.OrchardCore.Bookings;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Classifieds;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Commerce.Services;
using CrestApps.OrchardCore.Customers;
using CrestApps.OrchardCore.Downloads;
using CrestApps.OrchardCore.Fundraising;
using CrestApps.OrchardCore.Inventory;
using CrestApps.OrchardCore.Marketing;
using CrestApps.OrchardCore.Marketplace;
using CrestApps.OrchardCore.Memberships;
using CrestApps.OrchardCore.Orders;
using CrestApps.OrchardCore.PointOfSale;
using CrestApps.OrchardCore.Products.Feeds;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reviews;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// The setup guide's staff roles cover each job without more than it needs, through the stereotypes each module
/// declares for them, and its shipping step ships everywhere at the rate entered, once.
/// </summary>
public sealed class StoreSetupTests
{
    // Orchard Core's own permissions the Commerce module grants staff by name.
    private static readonly string[] _orchardCorePermissions =
    [
        "AccessAdminPanel", "ListContent", "ViewContent", "EditContent", "PublishContent", "DeleteContent", "ManageMediaContent",
    ];

    // Every commerce module that grants a staff role a permission.
    private static readonly IPermissionProvider[] _providers =
    [
        new CommercePermissionProvider(),
        new OrderPermissionsProvider(),
        new GiftCardPermissionsProvider(),
        new TransactionsPermissionProvider(),
        new InventoryPermissionsProvider(),
        new ShippingPermissionsProvider(),
        new CustomerPermissionsProvider(),
        new CheckoutPermissionsProvider(),
        new MarketingPermissionProvider(),
        new ReviewsPermissionsProvider(),
        new ProductFeedPermissionProvider(),
        new SubscriptionPermissionsProvider(),
        new BookingsPermissionProvider(),
        new MembershipsPermissionProvider(),
        new DownloadsPermissionsProvider(),
        new FundraisingPermissionProvider(),
        new ClassifiedsPermissionProvider(),
        new MarketplacePermissionProvider(),
        new PosPermissionProvider(),
        new ReportDesignerPermissionProvider(),
    ];

    [Fact]
    public void EveryStaffRole_OpensTheAdmin_AndHasAName_OfItsOwn()
    {
        Assert.Equal(StaffRolePresets.All.Count, StaffRolePresets.All.Select(preset => preset.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(StaffRolePresets.All, preset => Assert.Contains("AccessAdminPanel", PermissionsOf(preset.Name)));
    }

    [Fact]
    public async Task EveryPermissionAModuleGrantsStaff_IsOneItDeclares()
    {
        var roles = StaffRolePresets.All.Select(preset => preset.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in _providers)
        {
            var declared = (await provider.GetPermissionsAsync()).Select(permission => permission.Name).ToHashSet(StringComparer.Ordinal);
            var granted = provider.GetDefaultStereotypes()
                .Where(stereotype => roles.Contains(stereotype.Name))
                .SelectMany(stereotype => stereotype.Permissions)
                .Select(permission => permission.Name);

            Assert.All(granted, name => Assert.True(
                declared.Contains(name) || (provider is CommercePermissionProvider && _orchardCorePermissions.Contains(name)),
                $"{provider.GetType().Name} grants '{name}', which it doesn't declare."));
        }
    }

    [Fact]
    public void TheWarehouse_ShipsAndCountsStock_ButDoesntRefund_OrEditProducts()
    {
        var warehouse = PermissionsOf(CommerceConstants.StaffRoles.Warehouse);
        var marketing = PermissionsOf(CommerceConstants.StaffRoles.Marketing);
        var support = PermissionsOf(CommerceConstants.StaffRoles.CustomerSupport);
        var manager = PermissionsOf(CommerceConstants.StaffRoles.StoreManager);

        Assert.Contains("FulfillOrders", warehouse);
        Assert.Contains("ManageInventory", warehouse);
        Assert.DoesNotContain("ManageRefunds", warehouse);
        Assert.DoesNotContain("ManageOrders", warehouse);
        Assert.DoesNotContain("EditContent", warehouse);

        // Marketing edits products and promotions, not orders or money.
        Assert.Contains("ManageCoupons", marketing);
        Assert.Contains("ManageProductReviews", marketing);
        Assert.Contains("EditContent", marketing);
        Assert.DoesNotContain("ManageRefunds", marketing);
        Assert.DoesNotContain("ManageOrders", marketing);

        // The store manager does what the warehouse and support do, and runs the marketplace and the registers.
        Assert.All(warehouse.Concat(support), permission => Assert.Contains(permission, manager));
        Assert.Contains("ManageMarketplace", manager);
        Assert.Contains("ManageRegisters", manager);
        Assert.Contains("SellAtRegister", manager);
    }

    [Fact]
    public async Task TheStaffRolesStep_AddsTheMissingRoles_AndLeavesTheirPermissionsToTheStereotypes()
    {
        // Arrange
        var created = new List<Role>();
        var roleManager = new Mock<RoleManager<IRole>>(Mock.Of<IRoleStore<IRole>>(), null, null, null, null);
        roleManager.Setup(value => value.NormalizeKey(It.IsAny<string>())).Returns((string key) => key?.ToUpperInvariant());
        roleManager.Setup(value => value.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => string.Equals(name, CommerceConstants.StaffRoles.Warehouse, StringComparison.OrdinalIgnoreCase) ? new Role { RoleName = CommerceConstants.StaffRoles.Warehouse } : created.FirstOrDefault(role => string.Equals(role.RoleName, name, StringComparison.OrdinalIgnoreCase)));
        roleManager.Setup(value => value.CreateAsync(It.IsAny<IRole>()))
            .Callback((IRole role) => created.Add((Role)role))
            .ReturnsAsync(IdentityResult.Success);

        var step = new StaffRolesSetupStep(roleManager.Object, Mock.Of<LinkGenerator>(), new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, new PassThroughStringLocalizer<StaffRolesSetupStep>());

        // Act
        var result = await step.RunAsync(new StoreSetupActionContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal([CommerceConstants.StaffRoles.StoreManager, CommerceConstants.StaffRoles.CustomerSupport, CommerceConstants.StaffRoles.Marketing], created.Select(role => role.RoleName));
        Assert.All(created, role => Assert.False(string.IsNullOrEmpty(role.RoleDescription)));
        Assert.All(created, role => Assert.Empty(role.RoleClaims));
        Assert.True((await step.DescribeAsync(TestContext.Current.CancellationToken)).IsDone);
    }

    private static HashSet<string> PermissionsOf(string roleName)
        => _providers
            .SelectMany(provider => provider.GetDefaultStereotypes())
            .Where(stereotype => string.Equals(stereotype.Name, roleName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(stereotype => stereotype.Permissions)
            .Select(permission => permission.Name)
            .ToHashSet(StringComparer.Ordinal);

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

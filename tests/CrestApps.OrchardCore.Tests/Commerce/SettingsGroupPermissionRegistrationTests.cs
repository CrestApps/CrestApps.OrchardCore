using CrestApps.OrchardCore.Customers;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Orders;
using CrestApps.OrchardCore.Orders.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// Orchard Core's settings controller authorizes ManageGroupSettings with the group id as the resource, which only
/// a permission registered for the group with AddSiteSettingsPermission satisfies. Without it, a role that holds
/// the permission the settings menu item and display drivers check still gets 403 on /Admin/Settings/{groupId}.
/// </summary>
public sealed class SettingsGroupPermissionRegistrationTests
{
    [Fact]
    public void OrdersStartup_RegistersManageOrdersForTheOrdersSettingsGroup()
    {
        var permissions = GetGroupPermissions(new CrestApps.OrchardCore.Orders.Startup(), OrderConstants.SettingsGroupId);

        Assert.Contains(permissions, permission => permission.Name == OrderPermissions.ManageOrders.Name);
    }

    [Fact]
    public void CustomersStartup_RegistersManageCustomersForTheCustomersSettingsGroup()
    {
        var permissions = GetGroupPermissions(new CrestApps.OrchardCore.Customers.Startup(), CustomersConstants.SettingsGroupId);

        Assert.Contains(permissions, permission => permission.Name == CustomerPermissions.ManageCustomers.Name);
    }

    private static IList<Permission> GetGroupPermissions(StartupBase startup, string groupId)
    {
        var services = new ServiceCollection();
        startup.ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<SiteSettingsPermissionOptions>>().Value;

        Assert.True(options.GroupPermissions.TryGetValue(groupId, out var permissions), $"No permission is registered for the '{groupId}' settings group.");

        return permissions;
    }
}

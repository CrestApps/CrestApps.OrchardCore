using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.BackgroundTasks;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Handlers;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Drivers;
using CrestApps.OrchardCore.Orders.Handlers;
using CrestApps.OrchardCore.Orders.Migrations;
using CrestApps.OrchardCore.Orders.Navigation;
using CrestApps.OrchardCore.Orders.Services;
using Fluid;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Orders live in their own YesSql collection, which has to be declared for its documents to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(OrderConstants.CollectionName));
        services.AddDataMigration<OrderMigrations>()
            .AddIndexProvider<OrderIndexProvider>();

        services.AddScoped<IOrderStore, OrderStore>();
        services.AddScoped<IOrderService, OrderService>();

        // Unpaid orders whose checkout ended without them hearing of it are canceled.
        services.AddScoped<AbandonedOrderSweeper>();
        services.AddSingleton<IBackgroundTask, AbandonedOrdersBackgroundTask>();

        services.AddScoped<ICheckoutHandler, OrderCheckoutHandler>();
        services.AddScoped<ICheckoutHandler, OrderConfirmationCheckoutHandler>();
        services.AddScoped<OrderConfirmationEmailBuilder>();
        services.AddScoped<OrderLinksEmailBuilder>();
        services.AddScoped<IOrderLinkService, OrderLinkService>();
        services.AddDisplayDriver<CheckoutFlow, OrderConfirmationDisplayDriver>();

        services.AddScoped<IPermissionProvider, OrderPermissionsProvider>();
        services.AddNavigationProvider<OrdersAdminMenu>();
        services.AddDisplayDriver<Order, OrderDisplayDriver>();
        services.AddNavigationProvider<OrderSettingsAdminMenu>();
        services.AddSiteDisplayDriver<OrderSettingsDisplayDriver>();

        // Lets a role that holds only the manage orders permission open Settings > Commerce > Orders.
        services.AddSiteSettingsPermission(OrderConstants.SettingsGroupId, OrderPermissions.ManageOrders);

        services.Configure<TemplateOptions>(options =>
        {
            options.MemberAccessStrategy.Register<Order>();
            options.MemberAccessStrategy.Register<OrderLine>();
        });
    }
}

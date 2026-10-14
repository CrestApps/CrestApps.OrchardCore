using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Orders.Drivers;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Handlers;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Migrations;
using CrestApps.OrchardCore.Orders.Navigation;
using Fluid;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

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
        services.AddScoped<ICheckoutHandler, OrderCheckoutHandler>();
        services.AddDisplayDriver<CheckoutFlow, OrderConfirmationDisplayDriver>();

        services.AddScoped<IPermissionProvider, OrderPermissionsProvider>();
        services.AddNavigationProvider<OrdersAdminMenu>();

        services.Configure<TemplateOptions>(options =>
        {
            options.MemberAccessStrategy.Register<Order>();
            options.MemberAccessStrategy.Register<OrderLine>();
        });
    }
}

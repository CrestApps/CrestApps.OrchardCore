using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.BackgroundTasks;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Handlers;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Drivers;
using CrestApps.OrchardCore.Orders.Endpoints;
using CrestApps.OrchardCore.Orders.Handlers;
using CrestApps.OrchardCore.Orders.Migrations;
using CrestApps.OrchardCore.Orders.Navigation;
using CrestApps.OrchardCore.Orders.Services;
using Fluid;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Workflows.Helpers;
using CrestApps.OrchardCore.Orders.Workflows;
using CrestApps.OrchardCore.Orders.Reports;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Transactions.Services;

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
        services.AddNavigationProvider<OrdersAccountMenu>();
        services.AddSingleton<OrderAccountClaims>();
        services.AddDisplayDriver<Order, OrderDisplayDriver>();
        services.AddNavigationProvider<OrderSettingsAdminMenu>();
        services.AddSiteDisplayDriver<OrderSettingsDisplayDriver>();

        // Lets a role that holds only the manage orders permission open Settings > Commerce > Orders.
        services.AddSiteSettingsPermission(OrderConstants.SettingsGroupId, OrderPermissions.ManageOrders);

        // A canceled order gives back its coupon uses, and coupons can be limited to some products and categories.
        services.AddScoped<IOrderEventHandler, OrderCouponEventHandler>();

        // What the store does to an order from its page, and the refunds it records wherever they were started.
        services.AddScoped<OrderOperationScope>();
        services.AddScoped<OrderConfirmationSender>();
        services.AddScoped<OrderOperationsService>();
        services.AddScoped<IPaymentRefundHandler, OrderRefundRecorder>();
        services.AddScoped<ICheckoutReferenceDescriber, OrderCheckoutReferenceDescriber>();
        services.AddScoped<CouponItemLookup>();
        services.AddDisplayDriver<Coupon, CouponItemLimitsDisplayDriver>();

        services.Configure<TemplateOptions>(options =>
        {
            options.MemberAccessStrategy.Register<Order>();
            options.MemberAccessStrategy.Register<OrderLine>();
        });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.AddCouponItemSearchEndpoints();
}

/// <summary>
/// Adds the order reports to Reports > Commerce.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<OrderReportReader>();
        services.AddScoped<ProductCategoryNames>();
        services
            .AddScoped<IReport, OrderSalesReport>()
            .AddScoped<IReport, OrderCategorySalesReport>()
            .AddScoped<IReport, OrderCustomersReport>()
            .AddScoped<IReport, CheckoutConversionReport>()
            .AddScoped<IReport, OrderProductSalesReport>()
            .AddScoped<IReport, OrderCountrySalesReport>()
            .AddScoped<IReport, OrderTaxReport>()
            .AddScoped<IReport, OrderDiscountsReport>()
            .AddScoped<IReport, OrderRefundsReport>()
            .AddScoped<IReport, UnfulfilledOrdersReport>();
    }
}

/// <summary>
/// Starts workflows when orders are paid, canceled and refunded.
/// </summary>
[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<OrderPaidEvent, OrderEventDisplayDriver<OrderPaidEvent>>();
        services.AddActivity<OrderCanceledEvent, OrderEventDisplayDriver<OrderCanceledEvent>>();
        services.AddActivity<OrderRefundedEvent, OrderEventDisplayDriver<OrderRefundedEvent>>();
        services.AddScoped<IOrderEventHandler, OrderWorkflowHandler>();
    }
}

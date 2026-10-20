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
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
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
        services.AddScoped<DraftOrderService>();
        services.AddScoped<IOrderLinkService, OrderLinkService>();
        services.AddDisplayDriver<CheckoutFlow, OrderConfirmationDisplayDriver>();

        services.AddScoped<IPermissionProvider, OrderPermissionsProvider>();
        services.AddNavigationProvider<OrdersAdminMenu>();
        services.AddNavigationProvider<OrdersAccountMenu>();
        services.AddSingleton<OrderAccountClaims>();
        services.AddScoped<CheckoutRecoveryLinks>();
        services.AddScoped<CheckoutRecoveryService>();
        services.AddSingleton<IBackgroundTask, CheckoutRecoveryBackgroundTask>();
        services.AddDisplayDriver<Order, OrderDisplayDriver>();
        services.AddNavigationProvider<OrderSettingsAdminMenu>();
        services.AddSiteDisplayDriver<OrderSettingsDisplayDriver>();

        // The order bump on the payment step: one tick adds the product the store offers before paying.
        services.AddScoped<IDisplayDriver<CheckoutFlow>, OrderBumpCheckoutFlowDisplayDriver>();

        // Lets a role that holds only the manage orders permission open Settings > Commerce > Orders.
        services.AddSiteSettingsPermission(OrderConstants.SettingsGroupId, OrderPermissions.ManageOrders);

        // A canceled order gives back its coupon uses, and coupons can be limited to some products and categories.
        services.AddScoped<IOrderEventHandler, OrderCouponEventHandler>();

        // What the store does to an order from its page, and the refunds it records wherever they were started.
        services.AddScoped<OrderOperationScope>();
        services.AddScoped<OrderConfirmationSender>();
        services.AddScoped<OrderOperationsService>();
        services.AddScoped<CartCheckoutService>();

        // The order's invoice and packing slip, to print.
        services.AddScoped<OrderDocumentBuilder>();
        services.AddScoped<IPaymentRefundHandler, OrderRefundRecorder>();

        // A dispute reported by a gateway is recorded on its order, and shown on its page and in the orders list.
        services.AddScoped<CrestApps.OrchardCore.Payments.IPaymentEvent, OrderDisputeRecorder>();
        services.AddDisplayDriver<Order, OrderDisputesDisplayDriver>();

        // The fraud rating of an order's payment, kept on the order, shown on its page and in the orders list.
        services.AddScoped<ICheckoutHandler, OrderPaymentRiskCheckoutHandler>();
        services.AddDisplayDriver<Order, OrderPaymentRiskDisplayDriver>();
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
            .AddReport<OrderSalesReport>()
            .AddReport<OrderCategorySalesReport>()
            .AddReport<OrderCustomersReport>()
            .AddReport<CheckoutConversionReport>()
            .AddReport<OrderProductSalesReport>()
            .AddReport<OrderCountrySalesReport>()
            .AddReport<OrderTaxReport>()
            .AddReport<OrderDiscountsReport>()
            .AddReport<OrderRefundsReport>()
            .AddReport<OrderDisputesReport>()
            .AddReport<UnfulfilledOrdersReport>();
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

/// <summary>
/// Returns: buyers ask, the store approves, receives, restocks and refunds or exchanges.
/// </summary>
[Feature(OrderConstants.Features.Returns)]
public sealed class ReturnsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<OrderReturnMigrations>()
            .AddIndexProvider<OrderReturnIndexProvider>();
        services.AddScoped<IOrderReturnStore, OrderReturnStore>();
        services.AddScoped<OrderReturnService>();
        services.AddScoped<OrderReturnEmailSender>();
        services.AddDisplayDriver<Order, OrderReturnsDisplayDriver>();
        services.AddDisplayDriver<OrderReturn, OrderReturnDisplayDriver>();
        services.AddSiteDisplayDriver<ReturnSettingsDisplayDriver>();

        // Lets a role with Manage orders, but no site-settings permission, open Settings > Commerce > Returns.
        services.AddSiteSettingsPermission(OrderConstants.ReturnsSettingsGroupId, OrderPermissions.ManageOrders);
        services.AddNavigationProvider<ReturnsAdminMenu>();
    }
}

/// <summary>
/// Gift cards: sold as products, issued by hand, and paying toward checkouts after tax.
/// </summary>
[Feature(OrderConstants.Features.GiftCards)]
public sealed class GiftCardsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<GiftCardMigrations>()
            .AddIndexProvider<GiftCardIndexProvider>();
        services.AddScoped<IGiftCardStore, GiftCardStore>();
        services.AddScoped<GiftCardService>();
        services.AddScoped<GiftCardEmailSender>();
        services.AddScoped<GiftCardTenderProvider>();
        services.AddScoped<ICheckoutTenderProvider>(serviceProvider => serviceProvider.GetRequiredService<GiftCardTenderProvider>());
        services.AddScoped<IOrderEventHandler, GiftCardOrderEventHandler>();
        services.AddContentPart<GiftCardPart>()
            .UseDisplayDriver<GiftCardPartDisplayDriver>();
        services.AddDisplayDriver<GiftCard, GiftCardDisplayDriver>();
        services.AddScoped<GiftCardCheckoutService>();
        services.AddScoped<IDisplayDriver<CheckoutFlow>, GiftCardCheckoutFlowDisplayDriver>();
        services.AddScoped<IPermissionProvider, GiftCardPermissionsProvider>();
        services.AddNavigationProvider<GiftCardsAdminMenu>();
    }
}
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using CrestApps.OrchardCore.Checkout.Navigation;
using CrestApps.OrchardCore.Checkout.Migrations;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Handlers;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Core.Migrations;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Drivers;
using CrestApps.OrchardCore.Checkout.Endpoints;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.Tasks;
using CrestApps.OrchardCore.Payments;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Registers the provider-agnostic checkout and payment framework services.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<GuestSessionTokenManager>();
        services.AddScoped<ICheckoutSessionStore, CheckoutSessionStore>();
        services.AddScoped<IPaymentAttemptStore, PaymentAttemptStore>();
        services.AddScoped<IPaymentRefundStore, PaymentRefundStore>();
        services.AddScoped<ICheckoutReconciliationService, CheckoutReconciliationService>();
        services.AddScoped<ICheckoutEngine, DefaultCheckoutEngine>();
        services.AddScoped<ICheckoutPaymentProviderResolver, CheckoutPaymentProviderResolver>();
        services.AddScoped<ICheckoutRecurringPaymentProviderResolver, CheckoutRecurringPaymentProviderResolver>();

        // A site with no discounting registers no providers and the invoice is unchanged, so the seam costs
        // nothing until something uses it.
        services.AddScoped<ICheckoutDiscountService, DefaultCheckoutDiscountService>();

        // The coupon catalog is the discount provider shipped in the box. It is registered here rather than
        // in its own feature because the discount seam is worth nothing without at least one thing using it.
        // Coupons live in their own YesSql collection, so the collection must be declared for its document
        // table to exist. Without it the coupon catalog throws on the first read.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(CheckoutConstants.CouponCollectionName));

        services.AddDataMigration<CouponMigrations>()
            .AddIndexProvider<CouponIndexProvider>();

        services.AddScoped<ICouponStore, CouponStore>();
        services.AddScoped<ICheckoutDiscountProvider, CouponDiscountProvider>();
        services.AddScoped<ICheckoutHandler, CouponRedemptionCheckoutHandler>();
        services.AddScoped<IDisplayDriver<CheckoutFlow>, CouponCheckoutFlowDisplayDriver>();
        services.AddScoped<IPermissionProvider, CheckoutPermissionsProvider>();
        services.AddNavigationProvider<CouponsAdminMenu>();
        services.AddScoped<ICheckoutPaymentRefundProviderResolver, CheckoutPaymentRefundProviderResolver>();
        services.AddScoped<ICheckoutRefundService, DefaultCheckoutRefundService>();
        services.AddScoped<ICheckoutRefundReconciliationService, DefaultCheckoutRefundReconciliationService>();
        services.AddScoped<IPaymentEvent, RefundReconciliationPaymentEventHandler>();
        services.AddScoped<IPaymentAttemptLimiter, PaymentAttemptLimiter>();
        services.AddScoped<PaymentSessionCache>();
        services.AddScoped<CheckoutInvoiceBuilder>();
        services.AddScoped<ICheckoutHandler, PaymentCheckoutHandler>();
        services.AddSingleton<IBackgroundTask, CheckoutReconciliationBackgroundTask>();

        // The default tax service is a no-op. The Taxation integration feature replaces it with a
        // taxation-aware implementation when the Taxation feature is enabled.
        services.AddScoped<ICheckoutTaxService, NullCheckoutTaxService>();

        services.AddDataMigration<CheckoutMigrations>()
            .AddIndexProvider<CheckoutSessionIndexProvider>()
            .AddIndexProvider<PaymentAttemptIndexProvider>()
            .AddIndexProvider<PaymentRefundIndexProvider>();

        services.Configure<PaymentSessionCacheOptions>(options =>
        {
            options.MaxLiveSession = TimeSpan.FromHours(2);
        });

        services.Configure<PaymentRateLimitOptions>(_ => { });

        // The public checkout experience: the flow chrome, the payment step, and the client resources.
        services.AddScoped<IDisplayDriver<CheckoutFlow>, DefaultCheckoutFlowDisplayDriver>();
        services.AddScoped<IDisplayDriver<CheckoutFlow>, PaymentStepCheckoutFlowDisplayDriver>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, CheckoutResourceManagementOptionsConfiguration>();
    }

    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.AddCheckoutPaymentEndpoints();
}

using OrchardCore.Settings.Deployment;
using OrchardCore.RateLimits;
using OrchardCore.Environment.Shell.Configuration;
using CrestApps.OrchardCore.Throttling;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Commerce;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using CrestApps.OrchardCore.Checkout.Navigation;
using CrestApps.OrchardCore.Checkout.Migrations;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Handlers;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Drivers;
using CrestApps.OrchardCore.Checkout.Endpoints;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.Tasks;
using CrestApps.OrchardCore.Checkout.Deployments;
using CrestApps.OrchardCore.Checkout.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Recipes;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Checkout.Api;
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
using OrchardCore.Settings;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Customers.PersonalData;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Registers the provider-agnostic checkout and payment framework services.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The tenant's configuration, which can override attempt policies by name.</param>
    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<GuestSessionTokenManager>();
        services.AddScoped<ICheckoutSessionStore, CheckoutSessionStore>();
        services.AddScoped<IStoreSetupStep, CurrencySetupStep>();
        services.AddScoped<IStoreSetupStep, ProtectPaymentsSetupStep>();
        services.AddScoped<ICheckoutSessionAnonymizer, CheckoutSessionAnonymizer>();
        services.AddScoped<IPaymentAttemptStore, PaymentAttemptStore>();
        services.AddScoped<IPaymentRefundStore, PaymentRefundStore>();

        // Money operations that failed and must be finished by hand, listed under Commerce > Payment issues.
        services.AddPaymentIssues();
        services.AddDataMigration<PaymentIssueMigrations>()
            .AddIndexProvider<PaymentIssueIndexProvider>();
        services.AddScoped<ICheckoutReconciliationService, CheckoutReconciliationService>();
        services.AddScoped<ICheckoutEngine, DefaultCheckoutEngine>();

        // An edit sets aside a payment begun for the old total, so the buyer pays the new one.
        services.AddScoped<ICheckoutPaymentReleaseService, CheckoutPaymentReleaseService>();
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
            .AddIndexProvider<CouponIndexProvider>()
            .AddIndexProvider<CouponCodeIndexProvider>()
            .AddIndexProvider<CouponRedemptionIndexProvider>();

        services.AddScoped<ICouponStore, CouponStore>();
        services.AddScoped<CouponEditor>();
        services.AddScoped<CouponScopeEvaluator>();
        services.AddDisplayDriver<Coupon, CouponDisplayDriver>();
        services.AddScoped<ICheckoutDiscountProvider, CouponDiscountProvider>();
        services.AddScoped<ICheckoutHandler, CouponRedemptionCheckoutHandler>();
        services.AddScoped<ICheckoutHandler, DiscountLinkCheckoutHandler>();
        services.AddScoped<ICouponRedemptionService, CouponRedemptionService>();
        // The card-testing protection runs first: a refused payment holds no coupon use or balance.
        services.AddScoped<IPaymentDeclineRecorder, PaymentDeclineRecorder>();
        services.AddScoped<ICheckoutPaymentGate, PaymentVelocityGate>();
        services.AddSiteDisplayDriver<PaymentProtectionSettingsDisplayDriver>();
        services.AddScoped<ICheckoutPaymentGate, CouponHoldPaymentGate>();

        // Balances such as gift cards and store credit pay after tax. The seam does nothing until a feature registers
        // an ICheckoutTenderProvider. Its gate runs after the coupons', so it holds what's left after a code comes off.
        services.AddScoped<ICheckoutTenderService, DefaultCheckoutTenderService>();
        services.AddScoped<CheckoutTenderChoices>();
        services.AddScoped<ICheckoutTenderLedger, CheckoutTenderLedger>();
        services.AddScoped<ICheckoutPaymentGate, TenderHoldPaymentGate>();
        services.AddScoped<ICheckoutHandler, TenderCheckoutHandler>();
        services.AddDataMigration<CheckoutTenderMigrations>()
            .AddIndexProvider<CheckoutTenderHoldIndexProvider>();
        services.AddScoped<CheckoutCouponService>();
        services.AddDisplayDriver<CheckoutFlow, CouponCheckoutFlowDisplayDriver>();
        services.AddPermissionProvider<CheckoutPermissionsProvider>();
        services.AddNavigationProvider<CouponsAdminMenu>();
        services.AddSiteDisplayDriver<CheckoutSettingsDisplayDriver>();
        services.AddNavigationProvider<CheckoutSettingsAdminMenu>();
        services.AddSiteSettingsPermission(CheckoutConstants.SettingsGroupId, CheckoutPermissions.ManageCheckoutSettings);
        services.AddScoped<ICheckoutPaymentRefundProviderResolver, CheckoutPaymentRefundProviderResolver>();
        services.AddScoped<ICheckoutRefundService, DefaultCheckoutRefundService>();
        services.AddScoped<ICheckoutRefundReconciliationService, DefaultCheckoutRefundReconciliationService>();
        services.AddScoped<IPaymentEvent, RefundReconciliationPaymentEventHandler>();
        // The attempt policies are always on and need no middleware: card testing and code guessing are stopped whether
        // or not Orchard Core's Rate Limits feature is enabled. Each can be overridden by name in configuration.
        services
            .AddAttemptPolicy(CheckoutConstants.AttemptPolicies.PaymentBeginNetwork, 30, TimeSpan.FromMinutes(10))
            .AddAttemptPolicy(CheckoutConstants.AttemptPolicies.PaymentBeginCheckout, 8, TimeSpan.FromHours(24))
            .AddAttemptPolicy(CheckoutConstants.AttemptPolicies.PaymentDeclineNetwork, 5, TimeSpan.FromMinutes(30), blockDuration: TimeSpan.FromMinutes(30))
            .AddAttemptPolicy(CheckoutConstants.AttemptPolicies.PaymentDeclineCustomer, 5, TimeSpan.FromHours(1), blockDuration: TimeSpan.FromHours(1))
            .AddAttemptPolicy(CheckoutConstants.AttemptPolicies.PaymentDeclineStore, 30, TimeSpan.FromMinutes(10), blockDuration: TimeSpan.FromMinutes(30))
            .AddAttemptPolicy(CheckoutConstants.AttemptPolicies.PaymentStatus, 120, TimeSpan.FromMinutes(5))
            .AddAttemptPolicy(CheckoutConstants.AttemptPolicies.CheckoutStartNetwork, 60, TimeSpan.FromHours(1))
            .AddAttemptPolicy(CheckoutConstants.AttemptPolicies.CouponCode, 10, TimeSpan.FromMinutes(10), blockDuration: TimeSpan.FromMinutes(10))
            .AddAttemptPolicyOverrides(_shellConfiguration.GetSection(AttemptLimitOptions.SectionName));

#pragma warning disable CS0618 // The obsolete limiter stays registered for one release for code outside this repository.
        services.AddAttemptPolicy(PaymentAttemptLimiter.LegacyPolicyName, 20, TimeSpan.FromMinutes(5));
        services.AddScoped<IPaymentAttemptLimiter, PaymentAttemptLimiter>();
#pragma warning restore CS0618
        services.AddScoped<PaymentSessionCache>();
        services.AddScoped<CheckoutInvoiceBuilder>();
        services.AddScoped<ICheckoutHandler, PaymentCheckoutHandler>();
        services.AddSingleton<IBackgroundTask, CheckoutReconciliationBackgroundTask>();

        // The default tax service is a no-op. The Taxation integration feature replaces it with a
        // taxation-aware implementation when the Taxation feature is enabled. It is only a fallback: the order the
        // two startups run in depends on which other features are on, and adding it unconditionally after the
        // taxation-aware one would silently stop every checkout charging tax.
        services.TryAddScoped<ICheckoutTaxService, NullCheckoutTaxService>();

        services.AddDataMigration<CheckoutMigrations>()
            .AddIndexProvider<CheckoutSessionIndexProvider>()
            .AddIndexProvider<PaymentAttemptIndexProvider>()
            .AddIndexProvider<PaymentRefundIndexProvider>()
            .AddIndexProvider<CheckoutSessionCustomerIndexProvider>()
            .AddIndexProvider<CustomerPaymentMethodIndexProvider>();

        // The cards customers kept for charges made without them, filled by every checkout that keeps one.
        services.AddScoped<ISavedPaymentMethodStore, SavedPaymentMethodStore>();
        services.AddScoped<ICheckoutHandler, SavedPaymentMethodCheckoutHandler>();
        services.AddScoped<ICommercePersonalDataContributor, SavedPaymentMethodPersonalDataContributor>();

        services.AddScoped<ICommercePersonalDataContributor, CheckoutPersonalDataContributor>();
        services.AddScoped<ICommercePersonalDataContributor, CouponPersonalDataContributor>();

        // The public checkout experience: the flow chrome, the payment step, and the client resources.
        services.AddDisplayDriver<CheckoutFlow, DefaultCheckoutFlowDisplayDriver>();
        services.AddDisplayDriver<CheckoutFlow, PaymentStepCheckoutFlowDisplayDriver>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, CheckoutResourceManagementOptionsConfiguration>();
    }

    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.AddCheckoutPaymentEndpoints();
}

/// <summary>
/// Sets coupons from a recipe.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class CouponRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddRecipeExecutionStep<CouponsStep>();
}

/// <summary>
/// Exports coupons in a deployment plan.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class CouponDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDeployment<CouponsDeploymentSource, CouponsDeploymentStep, CouponsDeploymentStepDisplayDriver>();
}

/// <summary>
/// Exports the payment protection settings with a deployment; Orchard Core's Settings recipe step imports them.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class PaymentProtectionDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddSiteSettingsPropertyDeploymentStep<PaymentProtectionSettings, PaymentProtectionDeploymentStartup>(
            S => S["Payment protection settings"],
            S => S["What the store does when card payments are declined across the store."]);
}

[Feature(CheckoutConstants.Features.Api)]
public sealed class ApiStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapCheckoutApi();
        routes.MapCouponAdminApi();
    }
}

/// <summary>
/// Payment Rate Limits: Checkout's built-in group limits for Orchard Core's Rate Limits, and one JSON 429 body with a
/// Retry-After header for the routes they cover. They show read-only under Tools &gt; Rate Limits.
/// </summary>
[Feature(CheckoutConstants.Features.RateLimits)]
public sealed class PaymentRateLimitsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options => options
            .AddClientGroupRateLimit(CheckoutConstants.RateLimitGroups.Payment, 30, TimeSpan.FromMinutes(5), networkOnly: true)
            .AddClientGroupRateLimit(CheckoutConstants.RateLimitGroups.PaymentStatus, 60, TimeSpan.FromMinutes(1))
            .AddClientGroupRateLimit(CheckoutConstants.RateLimitGroups.Start, 30, TimeSpan.FromMinutes(10), networkOnly: true)
            .AddClientGroupRateLimit(CheckoutConstants.RateLimitGroups.Checkout, 120, TimeSpan.FromMinutes(1))
            .AddClientGroupRateLimit(CheckoutConstants.RateLimitGroups.Code, 20, TimeSpan.FromMinutes(5))
            .AddJsonRejectionHandler(
                CheckoutConstants.RouteNames.BeginPayment,
                CheckoutConstants.RouteNames.PaymentStatus,
                "BeginCheckoutPayment",
                "CompleteCheckout",
                "ApplyCheckoutCode",
                "ApplyCheckoutGiftCard",
                "StartCheckout"));
    }
}

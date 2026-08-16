using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Drivers;
using CrestApps.OrchardCore.Stripe.Endpoints;
using CrestApps.OrchardCore.Stripe.Indexes;
using CrestApps.OrchardCore.Stripe.Migrations;
using CrestApps.OrchardCore.Stripe.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using Stripe;

namespace CrestApps.OrchardCore.Stripe;

/// <summary>
/// Configures the Stripe module services and routes.
/// </summary>
public class Startup : StartupBase
{
    /// <summary>
    /// Registers Stripe services, settings drivers, permissions, migrations, indexes, and resource options.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDisplayDriver<ISite>, StripeSettingsDisplayDriver>();
        services.AddTransient<IConfigureOptions<StripeOptions>, StripeOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, ResourceManagementOptionsConfiguration>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddScoped<IPermissionProvider, StripePermissionsProvider>();

        services.AddDataMigration<StripeWebhookMigrations>();
        services.AddIndexProvider<ProcessedStripeWebhookEventIndexProvider>();

        services.AddScoped<IStripeSubscriptionService, StripeSubscriptionService>();
        services.AddScoped<IStripePaymentIntentService, StripePaymentIntentService>();
        services.AddScoped<IStripePaymentMethodService, StripePaymentMethodService>();
        services.AddScoped<IStripeProductService, StripeProductService>();
        services.AddScoped<IStripePriceService, StripePriceService>();
        services.AddScoped<IStripeSetupIntentService, StripeSetupIntentService>();
        services.AddScoped<IStripeCustomerService, StripeCustomerService>();
        services.AddScoped<IStripeCheckoutService, StripeCheckoutService>();
        services.AddScoped<IStripeRefundService, StripeRefundService>();
        services.AddScoped(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StripeOptions>>();

            // Enable Stripe.net's built-in exponential-backoff retries for transient failures and rate
            // limiting (HTTP 409/429/5xx). This keeps bulk price synchronization resilient to Stripe's
            // per-second request limits. Stripe.net reuses the idempotency key across automatic retries,
            // so retried create calls do not produce duplicate objects.
            var httpClient = new SystemNetHttpClient(maxNetworkRetries: 3);

            return new StripeClient(options.Value.ApiKey, httpClient: httpClient);
        });
    }

    /// <summary>
    /// Configures the Stripe webhook endpoint route.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="routes">The endpoint route builder.</param>
    /// <param name="serviceProvider">The application service provider.</param>
    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddWebhookEndpoint<Startup>();
    }
}

/// <summary>
/// Registers the generic Stripe checkout payment provider when the checkout framework is present, so any
/// checkout can collect and refund a card payment through Stripe without depending on the
/// subscription-specific Stripe endpoints.
/// </summary>
[RequireFeatures(CheckoutConstants.Features.Area)]
public sealed class CheckoutStartup : StartupBase
{
    /// <summary>
    /// Registers the Stripe checkout payment and refund provider.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<StripeCheckoutPaymentProvider>();
        services.AddScoped<ICheckoutPaymentProvider>(sp => sp.GetRequiredService<StripeCheckoutPaymentProvider>());
        services.AddScoped<ICheckoutPaymentRefundProvider>(sp => sp.GetRequiredService<StripeCheckoutPaymentProvider>());
    }
}

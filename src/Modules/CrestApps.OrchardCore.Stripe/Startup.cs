using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Drivers;
using CrestApps.OrchardCore.Stripe.Endpoints;
using CrestApps.OrchardCore.Stripe.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using Stripe;

namespace CrestApps.OrchardCore.Stripe;

public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDisplayDriver<ISite>, StripeSettingsDisplayDriver>();
        services.AddTransient<IConfigureOptions<StripeOptions>, StripeOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, ResourceManagementOptionsConfiguration>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddScoped<IPermissionProvider, StripePermissionsProvider>();

        services.AddScoped<IStripeSubscriptionService, StripeSubscriptionService>();
        services.AddScoped<IStripePaymentIntentService, StripePaymentIntentService>();
        services.AddScoped<IStripePaymentMethodService, StripePaymentMethodService>();
        services.AddScoped<IStripeProductService, StripeProductService>();
        services.AddScoped<IStripePriceService, StripePriceService>();
        services.AddScoped<IStripeSetupIntentService, StripeSetupIntentService>();
        services.AddScoped<IStripeCustomerService, StripeCustomerService>();
        services.AddScoped<IStripeCheckoutService, StripeCheckoutService>();
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

    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddCreateSetupIntentEndpoint()
            .AddCreatePaymentIntentEndpoint()
            .AddCreateSubscriptionEndpoint()
            .AddWebhookEndpoint<Startup>();
    }
}

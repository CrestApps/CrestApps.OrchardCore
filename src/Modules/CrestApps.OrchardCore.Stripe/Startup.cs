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
        services.AddScoped<INavigationProvider, AdminMenu>();
        services.AddScoped<IPermissionProvider, StripePermissionsProvider>();

        services.AddScoped<IStripeSubscriptionService, StripeSubscriptionService>();
        services.AddScoped<IStripePaymentService, StripePaymentService>();
        services.AddScoped<IStripeProductService, StripeProductService>();
        services.AddScoped<IStripePriceService, StripePriceService>();
        services.AddScoped<IStripeSetupIntentService, StripeSetupIntentService>();
        services.AddScoped<IStripeCustomerService, StripeCustomerService>();
        services.AddScoped(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StripeOptions>>();

            return new StripeClient(options.Value.ApiKey);
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

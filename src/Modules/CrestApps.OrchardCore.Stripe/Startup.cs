using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Drivers;
using CrestApps.OrchardCore.Stripe.Endpoints;
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

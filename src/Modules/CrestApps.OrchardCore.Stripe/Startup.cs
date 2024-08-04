using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Drivers;
using CrestApps.OrchardCore.Stripe.Endpoints.Intents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.ResourceManagement;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Stripe;

public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDisplayDriver<ISite>, StripeSettingsDisplayDriver>();
        services.AddTransient<IConfigureOptions<StripeOptions>, StripeOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, ResourceManagementOptionsConfiguration>();
    }

    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddCreateSetupIntentEndpoint()
            .AddCreatePaymentIntentEndpoint()
            .AddCreateSubscriptionEndpoint();
    }
}

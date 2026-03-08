using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Sms.Endpoints;
using CrestApps.OrchardCore.Omnichannel.Sms.Handlers;
using CrestApps.OrchardCore.Omnichannel.Sms.Indexes;
using CrestApps.OrchardCore.Omnichannel.Sms.Migrations;
using CrestApps.OrchardCore.Omnichannel.Sms.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Sms;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOmnichannelProcessor, SmsOmnichannelProcessor>());

        services.AddScoped<IOmnichannelEventHandler, SmsOmnichannelEventHandler>();

        services
            .AddDataMigration<OminchannelActivityAIChatSessionIndexMigrations>()
            .AddIndexProvider<OminchannelActivityAIChatSessionIndexProvider>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddTwilioWebhookEndpoint()
            .AddTwilioEventGridEndpoint();
    }
}


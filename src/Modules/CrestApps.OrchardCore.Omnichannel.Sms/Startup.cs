using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Sms.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Sms.Endpoints;
using CrestApps.OrchardCore.Omnichannel.Sms.Handlers;
using CrestApps.OrchardCore.Omnichannel.Sms.Indexes;
using CrestApps.OrchardCore.Omnichannel.Sms.Migrations;
using CrestApps.OrchardCore.Omnichannel.Sms.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Sms;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOmnichannelProcessor, SmsOmnichannelProcessor>());

        services.AddScoped<IOmnichannelEventHandler, SmsOmnichannelEventHandler>();

        // Re-drives automated SMS conversations whose in-memory reply generation was lost (for example on a restart),
        // so an owed reply is not left stranded and the no-response timeout does not wrongly fail the conversation.
        services.AddSingleton<IBackgroundTask, SmsOwedReplyRecoveryBackgroundTask>();

        // Proactively re-engages automated SMS contacts who have gone quiet (when the campaign enabled it), gated by
        // the campaign's business-hours calendar so nudges are never sent after hours.
        services.AddSingleton<IBackgroundTask, SmsReEngagementBackgroundTask>();

        services.AddRedaction(builder => builder.SetRedactor<ErasingRedactor>(LogDataClassifications.AddressSet));

        services
            .AddDataMigration<OminchannelActivityAIChatSessionIndexMigrations>()
            .AddIndexProvider<OminchannelActivityAIChatSessionIndexProvider>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddTwilioWebhookEndpoint();
    }
}

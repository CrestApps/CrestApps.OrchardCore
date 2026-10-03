using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Sms.Drivers;
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
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Sms.Services;

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

        // Asked before any text goes out, so a stop said on any record that holds the number is honoured here too.
        services.TryAddScoped<IContactOptOutResolver, ContactOptOutResolver>();

        // Automated texts leave through the provider that owns the sending number, picked exactly as the messaging
        // workspace picks it. The SMS Messaging Channel registers the same router, so TryAdd keeps one registration.
        services.TryAddScoped<ISmsProviderRouter, SmsProviderRouter>();

        // Re-drives automated SMS conversations whose in-memory reply generation was lost (for example on a restart),
        // so an owed reply is not left stranded and the no-response timeout does not wrongly fail the conversation.
        services.AddSingleton<IBackgroundTask, SmsOwedReplyRecoveryBackgroundTask>();

        // Proactively re-engages automated SMS contacts who have gone quiet (when the campaign enabled it), gated by
        // the campaign's business-hours calendar so nudges are never sent after hours.
        services.AddSingleton<IBackgroundTask, SmsReEngagementBackgroundTask>();

        services.AddRedaction(builder => builder.SetRedactor<ErasingRedactor>(LogDataClassifications.AddressSet));

        // The Twilio webhook, its settings address and the refusal logging live in the Omnichannel Twilio SMS
        // feature this one depends on, because the messaging workspace's SMS channel needs them without automation.

        services
            .AddDataMigration<OminchannelActivityAIChatSessionIndexMigrations>()
            .AddIndexProvider<OminchannelActivityAIChatSessionIndexProvider>();
    }
}

/// <summary>
/// Registers the Twilio inbound-SMS webhook. It is its own feature, enabled by both SMS Omnichannel Automation and the
/// SMS Messaging Channel, so a tenant receives Twilio texts with either one on, and the route is mapped once with both.
/// </summary>
[Feature(OmnichannelConstants.Features.TwilioSms)]
public sealed class TwilioSmsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Twilio says why it refused a message, and its provider throws that away. Recorded, so a text that fails
        // for credentials, region or a trial restriction says which in the log, and a refusal for an opted-out
        // recipient reaches the SMS dispatcher, which records the opt-out.
        services.AddTransient<TwilioErrorLoggingHandler>();
        services.AddHttpClient(TwilioSmsProvider.TechnicalName)
            .AddHttpMessageHandler<TwilioErrorLoggingHandler>();

        // Shows the inbound-SMS webhook address under Orchard Core's Twilio settings, beside the endpoint it names.
        services.AddDisplayDriver<ISite, TwilioSmsWebhookSettingsDisplayDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddTwilioWebhookEndpoint();
    }
}

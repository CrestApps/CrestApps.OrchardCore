using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Twilio;
using CrestApps.OrchardCore.Omnichannel.Sms.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Sms.Drivers;
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
/// Receives Twilio texts for the automated conversations. The SMS Messaging Channel registers the
/// same webhook for the workspace; whichever registers first owns it, so the route is mapped once with both on.
/// </summary>
/// <summary>
/// Lets a text entry point route its texts to an AI agent, which takes the customer's first text itself.
/// </summary>
[RequireFeatures(MessagingConstants.Feature.Sms)]
public sealed class EntryPointAIAgentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddEntryPointAIAgentChannel(OmnichannelConstants.Channels.Sms);
        services.AddDisplayDriver<ContactCenterEntryPoint, SmsEntryPointAIAgentDisplayDriver>();

        // Asked by both the automated handler and the messaging workspace, so it is scoped: the one instance remembers
        // what it decided for a text, and the second handler of the same text reads that answer.
        services.AddScoped<IMessagingAIConversationStarter, SmsEntryPointAIConversationStarter>();
    }
}

public sealed class TwilioSmsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => TwilioSmsWebhook.AddServices(services);

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => TwilioSmsWebhook.MapEndpoint(routes, serviceProvider);
}

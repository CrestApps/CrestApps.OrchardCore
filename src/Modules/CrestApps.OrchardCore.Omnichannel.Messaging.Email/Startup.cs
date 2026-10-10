using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.DeliveryEvents;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Drivers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Endpoints;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Mailbox;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Migrations;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email;

/// <summary>
/// Registers email as a channel of the messaging workspace: the channel, its sending transports, the inbound receiver
/// and its sources (provider webhooks and the mailbox reader), the email address capability and editor, the email entry
/// point channel, and the unsubscribe link.
/// </summary>
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMessagingChannel<EmailMessagingChannel>();

        // The channel resolves its dispatcher lazily: the dispatcher reads the addresses, and the address editor asks the
        // channel registry, so an eager dependency would close a cycle the container cannot report.
        services.AddScoped<IEmailDispatcher, EmailDispatcher>();
        services.AddScoped(sp => new Lazy<IEmailDispatcher>(sp.GetRequiredService<IEmailDispatcher>));
        services.AddScoped<IEmailSecretProtector, EmailSecretProtector>();
        services.AddScoped<IEmailUnsubscribeLinks, EmailUnsubscribeLinks>();
        services.AddScoped<IEmailOptOutService, EmailOptOutService>();
        services.AddScoped<IEmailWebhookUrls, EmailWebhookUrls>();
        services.AddScoped<IEmailConnectionTester, EmailConnectionTester>();

        // The ways an address can send. A provider's HTTP API is added by another feature with AddEmailTransport.
        services.AddEmailTransport<OrchardCoreEmailTransport>();
        services.AddEmailTransport<SmtpEmailTransport>();

        // Inbound email, from every source, is committed to the durable provider inbox keyed on its Message-ID before it
        // is processed, so a redelivered email is absorbed rather than recorded and answered twice.
        services.AddScoped<IEmailInboundReceiver, EmailInboundReceiver>();
        services.AddScoped<IProviderWebhookInboxHandler, EmailInboundInboxHandler>();
        services.AddScoped<IOmnichannelEventHandler, EmailReceivedMessagingEventHandler>();
        services.AddScoped<IMessagingInboundHandler, EmailAutomaticReplyInboundHandler>();

        // The provider formats the inbound webhook reads. Another provider is added with AddInboundEmailWebhookParser.
        services.AddInboundEmailWebhookParser<SendGridInboundEmailParser>();
        services.AddInboundEmailWebhookParser<MailgunInboundEmailParser>();
        services.AddInboundEmailWebhookParser<PostmarkInboundEmailParser>();
        services.AddInboundEmailWebhookParser<AmazonSesInboundEmailParser>();
        services.AddInboundEmailWebhookParser<MimeInboundEmailWebhookParser>();
        services.AddInboundEmailWebhookParser<JsonInboundEmailWebhookParser>();
        services.AddScoped<AmazonSnsMessageVerifier>();
        services.AddHttpClient(AmazonSnsMessageVerifier.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15));

        // Deliverability: every send is checked against the suppression list and, for bulk mail, against the address's
        // limits and pauses; every outcome is logged, and what providers report later (bounces, complaints, blocks)
        // suppresses addresses, pauses senders and slows mail to receiving domains that push back.
        // The log, the suppression list and the sending state live in a collection of their own, so a high-volume log
        // never shares a document table with the rest of the tenant's content.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(EmailChannelConstants.DeliverabilityCollectionName));
        services.AddIndexProvider<EmailDeliveryLogIndexProvider>();
        services.AddIndexProvider<EmailSuppressionIndexProvider>();
        services.AddIndexProvider<EmailSendingStateIndexProvider>();
        services.AddDataMigration<EmailDeliverabilityMigrations>();
        services.AddScoped<IEmailDeliveryLog, EmailDeliveryLog>();
        services.AddScoped<IEmailSuppressionList, EmailSuppressionList>();
        services.AddScoped<IEmailSendingStateStore, EmailSendingStateStore>();
        services.AddScoped<IEmailSentMessageFinder, EmailSentMessageFinder>();
        services.AddScoped<IEmailSendingGovernor, EmailSendingGovernor>();
        services.AddScoped<IEmailDeliveryEventProcessor, EmailDeliveryEventProcessor>();
        services.AddScoped<IProviderWebhookInboxHandler, EmailDeliveryEventsInboxHandler>();
        services.AddScoped<IOmnichannelSendPacer, EmailSendPacer>();
        services.AddScoped<IAutomatedActivityScreener, EmailSuppressionScreener>();
        services.AddEmailDeliveryEventParser<SendGridDeliveryEventParser>();
        services.AddEmailDeliveryEventParser<MailgunDeliveryEventParser>();
        services.AddEmailDeliveryEventParser<PostmarkDeliveryEventParser>();
        services.AddEmailDeliveryEventParser<AmazonSesDeliveryEventParser>();
        services.AddEmailDeliveryEventParser<JsonDeliveryEventParser>();
        services.AddSingleton<IBackgroundTask, EmailDeliveryLogPruningBackgroundTask>();
        services.AddNavigationProvider<EmailSuppressionsAdminMenu>();

        // The mailbox reader, for the mail hosts that cannot call a webhook.
        services.AddScoped<IEmailMailboxReader, ImapEmailMailboxReader>();
        services.AddScoped<IEmailMailboxPoller, EmailMailboxPoller>();
        services.AddSingleton<IBackgroundTask, EmailMailboxBackgroundTask>();

        // Email is something an email address does, so it is a capability this feature offers on the email addresses in
        // the address list, with the editor that says how each address sends and receives.
        services.AddOmnichannelAddressCapability(OmnichannelAddressTypes.EmailAddress, OmnichannelConstants.Channels.Email, capability =>
        {
            capability.DisplayName = S["Email"];
            capability.Description = S["Email sent and received on this address in the messaging workspace."];
            capability.CardName = S["Email"];
        });

        services.AddDisplayDriver<OmnichannelChannelEndpoint, EmailAddressSettingsDisplayDriver>();
        services.AddScoped<IChannelEndpointRule, EmailAddressSettingsRule>();
        services.AddSiteDisplayDriver<EmailInboundSettingsDisplayDriver>();

        // Email is a channel entry points answer: an email entry point says where mail to its addresses goes, with the
        // workspace's distribution and auto-reply settings.
        services.AddEntryPointChannel(OmnichannelConstants.Channels.Email, channel =>
        {
            channel.DisplayName = S["Email"];
            channel.Description = S["Answers email sent to its addresses: routes the conversations to a queue or an agent, with opening hours and auto-replies."];
        });

        // Activities can be loaded and worked on the Email channel.
        services.Configure<ActivityChannelOptions>(options =>
        {
            options.AddChannel(OmnichannelConstants.Channels.Email, entry =>
            {
                entry.DisplayName = S["Email"];
            });
        });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddEmailInboundWebhookEndpoint();
        routes.AddEmailUnsubscribeEndpoint();
        routes.AddEmailDeliveryEventsWebhookEndpoint();
    }
}

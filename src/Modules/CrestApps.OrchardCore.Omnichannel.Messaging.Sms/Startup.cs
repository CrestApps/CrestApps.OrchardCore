using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Drivers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Twilio;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms;

/// <summary>
/// Registers SMS as a channel of the messaging workspace: the channel itself, the per-number provider dispatcher,
/// the receiver that feeds inbound texts to the workspace, the carrier keywords, and the SMS endpoint editor.
/// </summary>
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(
        IStringLocalizer<Startup> stringLocalizer,
        IShellConfiguration shellConfiguration)
    {
        S = stringLocalizer;
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMessagingChannel<SmsMessagingChannel>();

        // The built-in SMS service sends through one tenant-default provider only, so a tenant whose numbers span
        // carriers needs the send routed to the provider that owns the sending number. The router that picks it is
        // shared with SMS Omnichannel Automation, which registers it too, so TryAdd keeps one registration.
        services.TryAddScoped<ISmsProviderRouter, SmsProviderRouter>();
        services.AddScoped<ISmsDispatcher, SmsDispatcher>();
        services.AddScoped(sp => new Lazy<ISmsDispatcher>(sp.GetRequiredService<ISmsDispatcher>));

        // OrchardCore's Twilio provider sends text only, so picture messages on a Twilio number go through this sender,
        // which also signs the download of a picture a customer sent to a Twilio number.
        services.AddScoped<TwilioSmsMediaSender>();
        services.AddScoped<ISmsMediaSender>(sp => sp.GetRequiredService<TwilioSmsMediaSender>());
        services.AddScoped<IMessagingMediaRequestAuthenticator>(sp => sp.GetRequiredService<TwilioSmsMediaSender>());
        services.AddHttpClient(TwilioSmsMediaSender.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));

        // Every SMS provider webhook raises SmsReceived on the shared Omnichannel event bus; this is what hands those
        // texts to the workspace.
        services.AddScoped<IOmnichannelEventHandler, SmsReceivedMessagingEventHandler>();

        // Inbound SMS is committed to the durable provider webhook inbox before it is processed, keyed on the
        // provider's own message id, so a redelivered text is absorbed rather than stored and answered twice.
        services.AddScoped<IProviderWebhookInboxHandler, SmsInboundInboxHandler>();

        // The carrier keywords a contact can text (STOP, START, HELP) and the replies they are owed.
        services.Configure<SmsKeywordReplySettings>(_shellConfiguration.GetSection("CrestApps:Omnichannel:Messaging:Sms:KeywordReplies"));
        services.AddScoped<IMessagingInboundHandler, SmsKeywordInboundHandler>();

        // Texting is something a phone number does, so it is a capability this feature offers on the phone numbers in
        // the address list, with the provider picker that pins a number's texts to the provider owning it.
        services.AddOmnichannelAddressCapability(OmnichannelAddressTypes.PhoneNumber, OmnichannelConstants.Channels.Sms, capability =>
        {
            capability.DisplayName = S["Text messages (SMS)"];
            capability.Description = S["Texts sent and received on this number in the messaging workspace."];
        });

        // Texts are a channel entry points answer: an SMS entry point says where texts to its numbers go, with the
        // workspace's distribution and auto-reply settings.
        services.AddEntryPointChannel(OmnichannelConstants.Channels.Sms, channel =>
        {
            channel.DisplayName = S["Text messages"];
            channel.Description = S["Answers texts to its numbers: routes the conversations to a queue or an agent, with opening hours and auto-replies."];
        });

        services.AddDisplayDriver<OmnichannelChannelEndpoint, SmsEndpointProviderDisplayDriver>();

        // Adds a "Send SMS" button next to phone-number fields on admin pages (mirrors the soft-phone dial button
        // and its PhoneFieldDialerShapeTableProvider). Injected from the phone field's own rendering via the shape
        // table, so only pages that actually show a phone field pay for it.
        services.AddShapeTableProvider<SmsPhoneFieldButtonShapeTableProvider>();
    }
}

/// <summary>
/// Receives Twilio texts for the workspace, so a tenant without SMS Omnichannel Automation still
/// hears from its Twilio numbers. SMS Omnichannel Automation registers the same webhook; whichever registers first owns
/// it, so the route is mapped once with both on.
/// </summary>
public sealed class TwilioSmsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => TwilioSmsWebhook.AddServices(services);

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => TwilioSmsWebhook.MapEndpoint(routes, serviceProvider);
}

using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Drivers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        // carriers needs the send routed to the provider that owns the sending number.
        services.AddScoped<ISmsDispatcher, SmsDispatcher>();
        services.AddScoped(sp => new Lazy<ISmsDispatcher>(sp.GetRequiredService<ISmsDispatcher>));

        // Every SMS provider webhook raises SmsReceived on the shared Omnichannel event bus; this is what hands those
        // texts to the workspace.
        services.AddScoped<IOmnichannelEventHandler, SmsReceivedMessagingEventHandler>();

        // Inbound SMS is committed to the durable provider webhook inbox before it is processed, keyed on the
        // provider's own message id, so a redelivered text is absorbed rather than stored and answered twice.
        services.AddScoped<IProviderWebhookInboxHandler, SmsInboundInboxHandler>();

        // The carrier keywords a contact can text (STOP, START, HELP) and the replies they are owed.
        services.Configure<SmsKeywordReplySettings>(_shellConfiguration.GetSection("CrestApps:Omnichannel:Messaging:Sms:KeywordReplies"));
        services.AddScoped<IMessagingInboundHandler, SmsKeywordInboundHandler>();

        // SMS numbers are channel endpoints. Register the SMS source for the endpoint screen and the provider picker
        // that pins a number to the provider owning it. The workspace's routing editor applies to these endpoints
        // because SMS is a registered messaging channel.
        services.AddChannelEndpointSource(OmnichannelConstants.Channels.Sms, source =>
        {
            source.DisplayName = S["SMS"];
            source.Description = S["A number that sends and receives text messages in the messaging workspace."];
        });

        services.AddDisplayDriver<OmnichannelChannelEndpoint, SmsEndpointProviderDisplayDriver>();

        // Adds a "Send SMS" button next to phone-number fields on admin pages (mirrors the soft-phone dial button
        // and its PhoneFieldDialerShapeTableProvider). Injected from the phone field's own rendering via the shape
        // table, so only pages that actually show a phone field pay for it.
        services.AddShapeTableProvider<SmsPhoneFieldButtonShapeTableProvider>();
    }
}

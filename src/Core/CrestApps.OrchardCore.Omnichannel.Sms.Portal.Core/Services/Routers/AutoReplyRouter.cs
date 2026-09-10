using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routers;

/// <summary>
/// Sends the endpoint's configured auto-reply. The setting was stored by the editor and sent by nothing, so an
/// operator could configure an acknowledgement, see it saved, and watch every contact get silence.
/// <para>
/// It runs first and never claims the conversation: the auto-reply is a side effect, not an ownership decision,
/// and claiming would stop the routers that actually place the thread from running at all.
/// </para>
/// </summary>
public sealed class AutoReplyRouter : ISmsInboundRouter
{
    private static readonly TimeSpan _minimumInterval = TimeSpan.FromDays(1);

    private readonly ISmsDispatcher _dispatcher;
    private readonly IContentManager _contentManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoReplyRouter"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher that sends the reply.</param>
    /// <param name="contentManager">The content manager, used to read the contact's opt-out state.</param>
    /// <param name="clock">The clock.</param>
    public AutoReplyRouter(ISmsDispatcher dispatcher, IContentManager contentManager, IClock clock)
    {
        _dispatcher = dispatcher;
        _contentManager = contentManager;
        _clock = clock;
    }

    /// <inheritdoc/>
    public int Order => 50;

    /// <inheritdoc/>
    public async Task<bool> TryRouteAsync(SmsRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Trigger != SmsRoutingTrigger.Inbound || context.Endpoint is null || context.Message is null)
        {
            return false;
        }

        // A keyword gets the one reply the carrier rules allow, sent by the keyword handler. An auto-reply on
        // top of it is a second message to someone who has just told us to stop.
        if (SmsKeywordPolicy.Classify(context.Message.Content) != SmsKeyword.None)
        {
            return false;
        }

        if (!context.Endpoint.TryGet<SmsEndpointRoutingSettings>(out var routing) ||
            routing is null ||
            string.IsNullOrWhiteSpace(routing.AutoReplyMessage))
        {
            return false;
        }

        var conversation = context.Conversation;
        var now = _clock.UtcNow;

        // Once a day per thread. A contact who sends three messages in a row should not get three
        // acknowledgements; that is a machine talking over someone trying to reach a person.
        if (conversation.LastAutoReplyUtc is not null && now - conversation.LastAutoReplyUtc < _minimumInterval)
        {
            return false;
        }

        // A contact who has opted out gets nothing automated, the acknowledgement included. The agent-facing
        // send path already refuses them; an automated reply that slipped past it would be the platform
        // texting someone who asked it not to.
        if (await IsOptedOutAsync(conversation, cancellationToken))
        {
            return false;
        }

        await _dispatcher.SendAsync(
            new SmsMessage
            {
                To = conversation.ContactAddress,
                From = conversation.ServiceAddress,
                Body = routing.AutoReplyMessage.Trim(),
            },
            cancellationToken);

        conversation.LastAutoReplyUtc = now;

        return false;
    }

    private async Task<bool> IsOptedOutAsync(SmsConversation conversation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            return false;
        }

        var contact = await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);

        return contact is not null && contact.TryGet<OmnichannelContactPart>(out var part) && part.DoNotSms;
    }
}

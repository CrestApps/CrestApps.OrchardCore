using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;

/// <summary>
/// Sends the endpoint's configured auto-reply, on whatever channel the conversation runs on. The setting was
/// stored by the editor and sent by nothing, so an operator could configure an acknowledgement, see it saved, and
/// watch every contact get silence.
/// <para>
/// It runs first and never claims the conversation: the auto-reply is a side effect, not an ownership decision,
/// and claiming would stop the routers that actually place the thread from running at all.
/// </para>
/// </summary>
public sealed class AutoReplyRouter : IMessagingInboundRouter
{
    private static readonly TimeSpan _minimumInterval = TimeSpan.FromDays(1);

    private readonly IContentManager _contentManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoReplyRouter"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager, used to read the contact's opt-out state.</param>
    /// <param name="clock">The clock.</param>
    public AutoReplyRouter(IContentManager contentManager, IClock clock)
    {
        _contentManager = contentManager;
        _clock = clock;
    }

    /// <inheritdoc/>
    public int Order => 50;

    /// <inheritdoc/>
    public async Task<bool> TryRouteAsync(MessagingRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Trigger != MessagingRoutingTrigger.Inbound ||
            context.Endpoint is null ||
            context.Message is null ||
            context.Channel is null)
        {
            return false;
        }

        // The channel has already given this message the one answer it is owed (a carrier keyword confirmation, for
        // instance). An auto-reply on top of it is a second message to someone who has just told us to stop.
        if (context.SuppressAutomatedReplies)
        {
            return false;
        }

        if (!context.Endpoint.TryGet<MessagingEndpointRoutingSettings>(out var routing) ||
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
        // messaging someone who asked it not to.
        if (await IsOptedOutAsync(context.Channel, conversation))
        {
            return false;
        }

        await context.Channel.SendAsync(
            new MessagingOutboundMessage
            {
                ContactAddress = conversation.ContactAddress,
                ServiceAddress = conversation.ServiceAddress,
                Body = routing.AutoReplyMessage.Trim(),
            },
            cancellationToken);

        conversation.LastAutoReplyUtc = now;

        return false;
    }

    private async Task<bool> IsOptedOutAsync(IMessagingChannel channel, MessagingConversation conversation)
    {
        if (string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            return false;
        }

        var contact = await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);

        return channel.IsOptedOut(contact);
    }
}

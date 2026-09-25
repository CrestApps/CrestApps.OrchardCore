using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Models;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;

/// <summary>
/// Applies the carrier keywords to an inbound text. STOP closes the thread, sets the contact's <c>Do not SMS</c>
/// flag and confirms; START reverses an opt-out and confirms; HELP says who is texting. These are not a product
/// feature: a contact who texts STOP is entitled to a confirmation and to hear nothing further, so the workspace's
/// own automation is silenced for a keyword and the one reply owed is sent here.
/// </summary>
public sealed class SmsKeywordInboundHandler : IMessagingInboundHandler
{
    private readonly ISmsDispatcher _dispatcher;
    private readonly IContentManager _contentManager;
    private readonly SmsKeywordReplySettings _keywordReplySettings;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsKeywordInboundHandler"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher the confirmation is sent through.</param>
    /// <param name="contentManager">The content manager, used to read and update the contact's opt-out.</param>
    /// <param name="keywordReplySettings">The tenant's keyword replies.</param>
    /// <param name="clock">The clock.</param>
    public SmsKeywordInboundHandler(
        ISmsDispatcher dispatcher,
        IContentManager contentManager,
        IOptions<SmsKeywordReplySettings> keywordReplySettings,
        IClock clock)
    {
        _dispatcher = dispatcher;
        _contentManager = contentManager;
        _keywordReplySettings = keywordReplySettings.Value;
        _clock = clock;
    }

    /// <inheritdoc/>
    public int Order => 0;

    /// <inheritdoc/>
    public Task ReceivingAsync(MessagingInboundContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A keyword gets the one reply the carrier rules allow, sent below. An auto-reply on top of it is a second
        // message to someone who has just told us to stop.
        if (IsSms(context) && SmsKeywordPolicy.Classify(context.Message.Content) != SmsKeyword.None)
        {
            context.SuppressAutomatedReplies = true;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task ReceivedAsync(MessagingInboundContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!IsSms(context))
        {
            return;
        }

        var keyword = SmsKeywordPolicy.Classify(context.Message.Content);

        if (keyword == SmsKeyword.None)
        {
            return;
        }

        var conversation = context.Conversation;

        switch (keyword)
        {
            case SmsKeyword.Stop:
                conversation.Status = ConversationStatus.Closed;
                await SetDoNotSmsAsync(conversation, true);

                break;

            case SmsKeyword.Start:
                // An opt-in keyword only means "make me reachable again" when the contact is actually opted out.
                // "YES" is one of them and is also the most ordinary answer there is — an automated agent opens
                // by asking a yes/no question, so the customer's "Yes." would otherwise be read as a resubscribe:
                // they get a confirmation they never asked for, landing as a second, unrelated message on top of
                // the agent's real reply. Nothing to reopen means nothing to confirm, so leave the message to the
                // conversation. STOP and HELP stay unconditional — those the carrier rules require us to answer
                // however the thread is going.
                if (!await IsOptedOutAsync(conversation))
                {
                    return;
                }

                // Reopening is the point: a contact who texts START is asking to be reachable again, and leaving
                // the thread closed would mean their next message arrives with no history attached.
                conversation.Status = ConversationStatus.Open;
                await SetDoNotSmsAsync(conversation, false);

                break;
        }

        var reply = SmsKeywordPolicy.ReplyFor(keyword, _keywordReplySettings);

        if (string.IsNullOrWhiteSpace(reply))
        {
            return;
        }

        // Sent through the dispatcher rather than the conversation service, because the conversation service
        // refuses to send to a contact who has opted out — and this confirmation is the one message that must
        // still reach them.
        await _dispatcher.SendAsync(
            new SmsMessage
            {
                To = conversation.ContactAddress,
                From = conversation.ServiceAddress,
                Body = reply,
            },
            cancellationToken);
    }

    private static bool IsSms(MessagingInboundContext context)
        => string.Equals(context.Channel?.Name, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the conversation's contact is currently opted out of SMS, which is what makes an
    /// opt-in keyword meaningful. A conversation with no contact record has no recorded opt-out to reverse.
    /// </summary>
    private async Task<bool> IsOptedOutAsync(MessagingConversation conversation)
    {
        if (string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            return false;
        }

        var contact = await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);

        // TryGet rather than As: a contact that has never carried the part should read as "not opted out", not
        // have an empty one created on it during what is only a question.
        return contact is not null
            && contact.TryGet<OmnichannelContactPart>(out var contactPart)
            && contactPart.DoNotSms;
    }

    private async Task SetDoNotSmsAsync(MessagingConversation conversation, bool doNotSms)
    {
        if (string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            return;
        }

        var contact = await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);

        if (contact is null)
        {
            return;
        }

        contact.Alter<OmnichannelContactPart>(part => part.SetDoNotSms(doNotSms, _clock.UtcNow));

        await _contentManager.UpdateAsync(contact);
    }
}

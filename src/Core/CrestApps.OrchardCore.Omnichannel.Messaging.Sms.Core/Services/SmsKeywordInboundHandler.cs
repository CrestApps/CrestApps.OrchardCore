using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
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
/// flag (or, when no contact holds the number, records the opt-out against the number) and confirms; START reverses
/// an opt-out and confirms; HELP says who is texting. These are not a product feature: a contact who texts STOP is
/// entitled to a confirmation and to hear nothing further, so the workspace's own automation is silenced for a
/// keyword and the one reply owed is sent here.
/// </summary>
public sealed class SmsKeywordInboundHandler : IMessagingInboundHandler
{
    /// <summary>
    /// The source recorded on an opt-out made by an inbound keyword.
    /// </summary>
    public const string OptOutSource = "SmsKeyword";

    private readonly ISmsDispatcher _dispatcher;
    private readonly IContentManager _contentManager;
    private readonly IContactOptOutResolver _optOutResolver;
    private readonly SmsKeywordReplySettings _keywordReplySettings;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsKeywordInboundHandler"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher the confirmation is sent through.</param>
    /// <param name="contentManager">The content manager, used to read and update the contact's opt-out.</param>
    /// <param name="optOutResolver">The resolver that keeps the opt-outs recorded against a number no contact holds.</param>
    /// <param name="keywordReplySettings">The tenant's keyword replies.</param>
    /// <param name="clock">The clock.</param>
    public SmsKeywordInboundHandler(
        ISmsDispatcher dispatcher,
        IContentManager contentManager,
        IContactOptOutResolver optOutResolver,
        IOptions<SmsKeywordReplySettings> keywordReplySettings,
        IClock clock)
    {
        _dispatcher = dispatcher;
        _contentManager = contentManager;
        _optOutResolver = optOutResolver;
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
                await SetOptedOutAsync(conversation, true, cancellationToken);

                break;

            case SmsKeyword.Start:
                // An opt-in keyword only means "make me reachable again" when the contact is actually opted out.
                // "YES" is one of them and is also the most ordinary answer there is — an automated agent opens
                // by asking a yes/no question, so the customer's "Yes." would otherwise be read as a resubscribe:
                // they get a confirmation they never asked for, landing as a second, unrelated message on top of
                // the agent's real reply. Nothing to reopen means nothing to confirm, so leave the message to the
                // conversation. STOP and HELP stay unconditional — those the carrier rules require us to answer
                // however the thread is going.
                if (!await IsOptedOutAsync(conversation, cancellationToken))
                {
                    return;
                }

                // Reopening is the point: a contact who texts START is asking to be reachable again, and leaving
                // the thread closed would mean their next message arrives with no history attached.
                conversation.Status = ConversationStatus.Open;
                await SetOptedOutAsync(conversation, false, cancellationToken);

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
    /// Determines whether the conversation's contact, or its number when no contact holds it, is currently opted out
    /// of SMS, which is what makes an opt-in keyword meaningful.
    /// </summary>
    private async Task<bool> IsOptedOutAsync(MessagingConversation conversation, CancellationToken cancellationToken)
    {
        if (await _optOutResolver.HasAddressOptedOutAsync(OmnichannelConstants.Channels.Sms, conversation.ContactAddress, cancellationToken))
        {
            return true;
        }

        var contact = await GetContactAsync(conversation);

        // TryGet rather than As: a contact that has never carried the part should read as "not opted out", not
        // have an empty one created on it during what is only a question.
        return contact is not null
            && contact.TryGet<OmnichannelContactPart>(out var contactPart)
            && contactPart.DoNotSms;
    }

    private async Task SetOptedOutAsync(MessagingConversation conversation, bool optedOut, CancellationToken cancellationToken)
    {
        var contact = await GetContactAsync(conversation);

        if (optedOut && contact is null)
        {
            // Nobody on file holds the number, but the person who texted STOP is owed silence on it all the same, so
            // the opt-out is recorded against the number. Every send path asks the opt-out resolver about it.
            await _optOutResolver.RecordAddressOptOutAsync(
                new ContactAddressOptOut
                {
                    Channel = OmnichannelConstants.Channels.Sms,
                    Address = conversation.ContactAddress,
                    Source = OptOutSource,
                    OptedOutUtc = _clock.UtcNow,
                },
                cancellationToken);

            return;
        }

        if (!optedOut)
        {
            // An opt-out recorded while no contact held the number is lifted too, whoever holds the number now.
            await _optOutResolver.ClearAddressOptOutAsync(OmnichannelConstants.Channels.Sms, conversation.ContactAddress, cancellationToken);
        }

        if (contact is null)
        {
            return;
        }

        contact.Alter<OmnichannelContactPart>(part => part.SetDoNotSms(optedOut, _clock.UtcNow));

        await _contentManager.UpdateAsync(contact);
    }

    private async Task<ContentItem> GetContactAsync(MessagingConversation conversation)
    {
        if (string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            return null;
        }

        return await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);
    }
}

using System.Globalization;
using System.Security.Claims;
using System.Text;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tickets.Messaging;

/// <summary>
/// What a messaging conversation gives a new ticket: the customer as the requester (their contact, else their address),
/// a subject, and the latest messages as the first message, for an agent who may read the conversation.
/// </summary>
public sealed class TicketConversationSource
{
    /// <summary>
    /// The record kind a ticket links its conversation with.
    /// </summary>
    public const string RecordKind = "MessagingConversation";

    // How many of the latest messages the first message quotes.
    private const int MessageCount = 30;

    private readonly IMessagingConversationStore _conversations;
    private readonly IAuthorizationService _authorizationService;
    private readonly IContentManager _contentManager;
    private readonly ISession _session;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public TicketConversationSource(
        IMessagingConversationStore conversations,
        IAuthorizationService authorizationService,
        IContentManager contentManager,
        ISession session,
        ILocalClock localClock,
        IStringLocalizer<TicketConversationSource> stringLocalizer)
    {
        _conversations = conversations;
        _authorizationService = authorizationService;
        _contentManager = contentManager;
        _session = session;
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns what a conversation gives a new ticket, or <see langword="null"/> when it doesn't exist or the user may not
    /// read it.
    /// </summary>
    /// <param name="user">The agent.</param>
    /// <param name="conversationId">The conversation.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The seed, or <see langword="null"/>.</returns>
    public async Task<TicketConversationSeed> GetAsync(ClaimsPrincipal user, string conversationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(conversationId)
            || await _conversations.FindByIdAsync(conversationId, cancellationToken) is not { } conversation
            || !await _authorizationService.AuthorizeConversationAsync(user, conversation, ConversationOperation.View))
        {
            return null;
        }

        var contact = string.IsNullOrEmpty(conversation.ContactContentItemId)
            ? null
            : await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);
        var name = contact?.DisplayText ?? conversation.ContactAddress;

        var messages = (await _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(index => index.ConversationId == conversationId, collection: OmnichannelConstants.CollectionName)
                .OrderByDescending(index => index.CreatedUtc)
                .Take(MessageCount)
                .ListAsync(cancellationToken))
            .Reverse()
            .ToList();

        var body = new StringBuilder();
        body.AppendLine(S["Conversation on {0} with {1}:", conversation.Channel, name].Value);

        foreach (var message in messages.Where(message => !string.IsNullOrWhiteSpace(message.Content)))
        {
            var when = (await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(message.CreatedUtc, DateTimeKind.Utc)))).ToString("g", CultureInfo.CurrentCulture);
            body.AppendLine().Append(when).Append(' ').Append(message.IsInbound ? name : S["Agent"].Value).Append(": ").AppendLine(message.Content.Trim());
        }

        return new TicketConversationSeed(
            conversation.ItemId,
            contact is null ? default : new PartyRef(PartyKinds.Contact, contact.ContentItemId),
            name,
            S["Follow-up of the conversation with {0}", name].Value,
            body.ToString().Trim());
    }
}

/// <summary>
/// What a conversation gives a new ticket.
/// </summary>
/// <param name="ConversationId">The conversation.</param>
/// <param name="Requester">The customer's contact, if known.</param>
/// <param name="CustomerName">The customer's name, or their address.</param>
/// <param name="Subject">A subject.</param>
/// <param name="Body">The latest messages.</param>
public sealed record TicketConversationSeed(string ConversationId, PartyRef Requester, string CustomerName, string Subject, string Body);

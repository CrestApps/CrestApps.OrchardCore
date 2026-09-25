using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The default <see cref="IMessagingConversationService"/>: authorizes agent replies and sends them through the
/// conversation's own channel, and reconciles provider delivery receipts back onto the sent message.
/// </summary>
public sealed class MessagingConversationService : IMessagingConversationService
{
    private readonly IMessagingConversationStore _conversationStore;
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IContentManager _contentManager;
    private readonly IMessagingContactResolver _contactResolver;
    private readonly IMessagingRealTimeNotifier _notifier;
    private readonly IMessagingConversationAuthorizationService _conversationAuthorizationService;
    private readonly ISession _session;
    private readonly IMessagingFirstResponseSlaService _slaService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingConversationService"/> class.
    /// </summary>
    public MessagingConversationService(
        IMessagingConversationStore conversationStore,
        IMessagingChannelResolver channelResolver,
        IContentManager contentManager,
        IMessagingContactResolver contactResolver,
        IMessagingRealTimeNotifier notifier,
        IMessagingConversationAuthorizationService conversationAuthorizationService,
        ISession session,
        IMessagingFirstResponseSlaService slaService,
        IClock clock,
        ILogger<MessagingConversationService> logger)
    {
        _conversationStore = conversationStore;
        _channelResolver = channelResolver;
        _contentManager = contentManager;
        _contactResolver = contactResolver;
        _notifier = notifier;
        _conversationAuthorizationService = conversationAuthorizationService;
        _session = session;
        _slaService = slaService;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MessagingSendResult> SendAsync(MessagingSendRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Body) && (request.MediaUrls is null || request.MediaUrls.Count == 0))
        {
            return MessagingSendResult.Failed("The message body is required.");
        }

        var conversation = await _conversationStore.FindByIdAsync(request.ConversationId, cancellationToken);

        if (conversation is null)
        {
            return MessagingSendResult.Failed("The conversation was not found.");
        }

        var channel = _channelResolver.Get(conversation.Channel);

        if (channel is null)
        {
            return MessagingSendResult.Failed($"The '{conversation.Channel}' channel is not enabled.");
        }

        if (!await IsPrincipalAuthorizedAsync(request.Principal, conversation, ConversationOperation.Send, cancellationToken))
        {
            return MessagingSendResult.Failed("You are not allowed to send on this conversation.");
        }

        // Enforce the contact's opt-out of this channel on every send.
        if (await IsOptedOutAsync(channel, conversation))
        {
            return MessagingSendResult.Failed($"The contact has opted out of {channel.DisplayName.Value}.");
        }

        var message = CreateOutboundMessage(conversation, request.Body, request.ActingAgentId);
        message.MediaReferences = request.MediaUrls?.ToList() ?? [];

        var dispatch = await channel.SendAsync(new MessagingOutboundMessage
        {
            ServiceAddress = conversation.ServiceAddress,
            ContactAddress = conversation.ContactAddress,
            Subject = request.Subject,
            Body = request.Body,
            MediaUrls = request.MediaUrls ?? [],
        }, cancellationToken);

        ApplyDispatchOutcome(message, dispatch, conversation.ItemId);

        await _session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);

        // Sending marks the thread read and, on an unassigned personal claim, assigns it to the acting agent.
        conversation.LastMessageUtc = message.CreatedUtc;
        conversation.LastMessagePreview = MessagingConversationRollup.BuildPreview(request.Body);
        conversation.IsRead = true;
        conversation.UnreadCount = 0;
        // The agent engaged, so a routed thread is picked up and no longer subject to reassignment, and the
        // customer is no longer waiting for a first reply.
        conversation.AssignedUtc = null;
        conversation.ReassignmentAttempts = 0;
        conversation.ModifiedUtc = _clock.UtcNow;
        _slaService.MarkResponded(conversation);

        if (!string.IsNullOrEmpty(request.ActingAgentId) &&
            conversation.OwnerType == ConversationOwnerType.Personal &&
            conversation.AssignmentStatus == ConversationAssignmentStatus.Unassigned)
        {
            conversation.OwnerId = request.ActingAgentId;
            conversation.AssignedAgentId = request.ActingAgentId;
            conversation.AssignmentStatus = ConversationAssignmentStatus.Assigned;
        }

        await _conversationStore.UpdateAsync(conversation, cancellationToken);

        return new MessagingSendResult
        {
            Succeeded = dispatch.Succeeded,
            Message = message,
            Error = dispatch.Succeeded ? null : message.ErrorCode,
        };
    }

    /// <inheritdoc/>
    public async Task<MessagingSendResult> SendDirectAsync(string channel, string serviceAddress, string contactAddress, string body, string actingAgentId, CancellationToken cancellationToken = default)
    {
        var messagingChannel = _channelResolver.Get(channel);

        if (messagingChannel is null)
        {
            return MessagingSendResult.Failed($"The '{channel}' channel is not enabled.");
        }

        if (string.IsNullOrWhiteSpace(serviceAddress) || string.IsNullOrWhiteSpace(contactAddress))
        {
            return MessagingSendResult.Failed("Both a sending address and a recipient are required.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return MessagingSendResult.Failed("The message body is required.");
        }

        serviceAddress = messagingChannel.NormalizeAddress(serviceAddress);
        contactAddress = messagingChannel.NormalizeAddress(contactAddress);

        // Enforce a single conversation per contact address on the channel: reuse any existing thread for this
        // contact (on any of our endpoints) rather than creating a duplicate; only create when none exists.
        var conversation = await _conversationStore.FindByContactAsync(messagingChannel.Name, contactAddress, cancellationToken);
        var isNew = conversation is null;

        if (isNew)
        {
            conversation = new MessagingConversation
            {
                ItemId = UniqueId.GenerateId(),
                Channel = messagingChannel.Name,
                ServiceAddress = serviceAddress,
                ContactAddress = contactAddress,
                Status = ConversationStatus.Open,
                OwnerType = ConversationOwnerType.Personal,
                OwnerId = actingAgentId,
                AssignedAgentId = actingAgentId,
                AssignmentStatus = string.IsNullOrEmpty(actingAgentId)
                    ? ConversationAssignmentStatus.Unassigned
                    : ConversationAssignmentStatus.Assigned,
                CreatedUtc = _clock.UtcNow,
                ContactContentItemId = await _contactResolver.ResolveContactContentItemIdAsync(messagingChannel.Name, contactAddress, cancellationToken),
            };
        }

        if (await IsOptedOutAsync(messagingChannel, conversation))
        {
            return MessagingSendResult.Failed($"The contact has opted out of {messagingChannel.DisplayName.Value}.");
        }

        var message = CreateOutboundMessage(conversation, body, actingAgentId);

        var dispatch = await messagingChannel.SendAsync(new MessagingOutboundMessage
        {
            ServiceAddress = conversation.ServiceAddress,
            ContactAddress = conversation.ContactAddress,
            Body = body,
        }, cancellationToken);

        ApplyDispatchOutcome(message, dispatch, conversation.ItemId);

        await _session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);

        conversation.LastMessageUtc = message.CreatedUtc;
        conversation.LastMessagePreview = MessagingConversationRollup.BuildPreview(body);
        conversation.IsRead = true;
        conversation.ModifiedUtc = _clock.UtcNow;
        _slaService.MarkResponded(conversation);

        // Reopen a closed/snoozed thread when the agent messages the contact again.
        if (!isNew && conversation.Status is ConversationStatus.Closed or ConversationStatus.Snoozed)
        {
            conversation.Status = ConversationStatus.Open;
        }

        if (isNew)
        {
            await _conversationStore.CreateAsync(conversation, cancellationToken);
        }
        else
        {
            await _conversationStore.UpdateAsync(conversation, cancellationToken);
        }

        return new MessagingSendResult
        {
            Succeeded = dispatch.Succeeded,
            Message = message,
            Error = dispatch.Succeeded ? null : message.ErrorCode,
        };
    }

    /// <inheritdoc/>
    public async Task<bool> ApplyDeliveryReceiptAsync(MessageDeliveryReceipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var channel = _channelResolver.Get(receipt.Channel);

        if (channel is null)
        {
            return false;
        }

        var serviceAddress = channel.NormalizeAddress(receipt.ServiceAddress);
        var contactAddress = channel.NormalizeAddress(receipt.ContactAddress);

        if (string.IsNullOrEmpty(serviceAddress) || string.IsNullOrEmpty(contactAddress))
        {
            return false;
        }

        var conversation = await _conversationStore.FindByAddressesAsync(channel.Name, serviceAddress, contactAddress, cancellationToken);

        if (conversation is null)
        {
            return false;
        }

        // The provider message id is indexed, so a receipt that carries one finds its message in a single seek
        // rather than by loading a thread's whole outbound history and matching in memory.
        var channelName = channel.Name;

        var message = !string.IsNullOrEmpty(receipt.ProviderMessageId)
            ? await _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                    index => index.Channel == channelName &&
                        index.ProviderMessageId == receipt.ProviderMessageId,
                    collection: OmnichannelConstants.CollectionName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        if (message is null)
        {
            // A provider that reports a receipt before the send recorded its id (or one that reports none at all)
            // still has to land somewhere, so fall back to the newest outbound message with no id yet.
            var outbound = (await _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                    index => index.ConversationId == conversation.ItemId && !index.IsInbound,
                    collection: OmnichannelConstants.CollectionName)
                .OrderByDescending(index => index.CreatedUtc)
                .ListAsync(cancellationToken))
                .ToArray();

            message = !string.IsNullOrEmpty(receipt.ProviderMessageId)
                ? outbound.FirstOrDefault(m => string.IsNullOrEmpty(m.ProviderMessageId))
                : outbound.FirstOrDefault();
        }

        if (message is null)
        {
            return false;
        }

        message.DeliveryStatus = receipt.Status.ToString();
        message.ErrorCode = receipt.ErrorCode;

        if (!string.IsNullOrEmpty(receipt.ProviderMessageId))
        {
            message.ProviderMessageId = receipt.ProviderMessageId;
        }

        await _session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);

        await _notifier.MessageDeliveryUpdatedAsync(new MessagingDeliveryNotification
        {
            ConversationId = conversation.ItemId,
            MessageId = message.Id,
            Status = receipt.Status,
            ErrorCode = receipt.ErrorCode,
            AssignedAgentId = conversation.AssignedAgentId,
            OwnerQueueId = conversation.OwnerType == ConversationOwnerType.Queue ? conversation.OwnerId : null,
        }, cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public async Task<MessagingSendResult> ClaimAsync(string conversationId, string actingAgentId, ClaimsPrincipal principal = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(actingAgentId))
        {
            return MessagingSendResult.Failed("An agent is required to claim a conversation.");
        }

        var conversation = await _conversationStore.FindByIdAsync(conversationId, cancellationToken);

        if (conversation is null)
        {
            return MessagingSendResult.Failed("The conversation was not found.");
        }

        if (!await IsPrincipalAuthorizedAsync(principal, conversation, ConversationOperation.Claim, cancellationToken))
        {
            return MessagingSendResult.Failed("You are not allowed to claim this conversation.");
        }

        if (conversation.AssignmentStatus == ConversationAssignmentStatus.Assigned &&
            !string.IsNullOrEmpty(conversation.AssignedAgentId) &&
            conversation.AssignedAgentId != actingAgentId)
        {
            return MessagingSendResult.Failed("The conversation has already been claimed by another agent.");
        }

        return await AssignInternalAsync(conversation, actingAgentId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<MessagingSendResult> AssignAsync(string conversationId, string targetAgentId, ClaimsPrincipal principal = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(targetAgentId))
        {
            return MessagingSendResult.Failed("A target agent is required.");
        }

        var conversation = await _conversationStore.FindByIdAsync(conversationId, cancellationToken);

        if (conversation is null)
        {
            return MessagingSendResult.Failed("The conversation was not found.");
        }

        if (!await IsPrincipalAuthorizedAsync(principal, conversation, ConversationOperation.Transfer, cancellationToken))
        {
            return MessagingSendResult.Failed("You are not allowed to transfer this conversation.");
        }

        return await AssignInternalAsync(conversation, targetAgentId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<MessagingSendResult> SetStatusAsync(string conversationId, ConversationStatus status, ClaimsPrincipal principal = null, CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationStore.FindByIdAsync(conversationId, cancellationToken);

        if (conversation is null)
        {
            return MessagingSendResult.Failed("The conversation was not found.");
        }

        var operation = status switch
        {
            ConversationStatus.Snoozed => ConversationOperation.Snooze,
            ConversationStatus.Open => ConversationOperation.View,
            _ => ConversationOperation.Close,
        };

        if (!await IsPrincipalAuthorizedAsync(principal, conversation, operation, cancellationToken))
        {
            return MessagingSendResult.Failed("You are not allowed to change the status of this conversation.");
        }

        conversation.Status = status;
        conversation.ModifiedUtc = _clock.UtcNow;

        await _conversationStore.UpdateAsync(conversation, cancellationToken);

        return new MessagingSendResult { Succeeded = true };
    }

    private OmnichannelMessage CreateOutboundMessage(MessagingConversation conversation, string body, string actingAgentId)
        => new()
        {
            Id = UniqueId.GenerateId(),
            Channel = conversation.Channel,
            CustomerAddress = conversation.ContactAddress,
            ServiceAddress = conversation.ServiceAddress,
            Content = body,
            CreatedUtc = _clock.UtcNow,
            IsInbound = false,
            ConversationId = conversation.ItemId,
            SentByAgentId = actingAgentId,
            DeliveryStatus = MessageDeliveryStatus.Queued.ToString(),
        };

    private async Task<MessagingSendResult> AssignInternalAsync(MessagingConversation conversation, string agentId, CancellationToken cancellationToken)
    {
        conversation.AssignedAgentId = agentId;
        conversation.AssignmentStatus = ConversationAssignmentStatus.Assigned;

        // A personal (non-queue) conversation follows its assignee as owner; a queue conversation keeps the
        // queue as its owner so it stays discoverable to the department.
        if (conversation.OwnerType == ConversationOwnerType.Personal)
        {
            conversation.OwnerId = agentId;
        }

        conversation.ModifiedUtc = _clock.UtcNow;

        await _conversationStore.UpdateAsync(conversation, cancellationToken);

        await _notifier.ConversationAssignedAsync(new MessagingAssignmentNotification
        {
            ConversationId = conversation.ItemId,
            AssignedAgentId = conversation.AssignedAgentId,
            OwnerQueueId = conversation.OwnerType == ConversationOwnerType.Queue ? conversation.OwnerId : null,
        }, cancellationToken);

        return new MessagingSendResult { Succeeded = true };
    }

    // The last line of defence behind the controller and hub checks: when the caller is a user (not a system
    // path such as an auto-reply, a broadcast fan-out, or an AI handoff), the conversation-level rule is applied
    // again here so a caller that reached the service directly cannot act on a thread it does not own or serve.
    private async Task<bool> IsPrincipalAuthorizedAsync(
        ClaimsPrincipal principal,
        MessagingConversation conversation,
        ConversationOperation operation,
        CancellationToken cancellationToken)
    {
        if (principal is null)
        {
            return true;
        }

        return await _conversationAuthorizationService.AuthorizeAsync(principal, conversation, operation, cancellationToken);
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

    // Records what the provider said about one attempt. An accepted message stores the provider's own id so a
    // later receipt matches it exactly; a refused one is left Queued with a scheduled retry until the backoff
    // schedule is exhausted, at which point it becomes a visible failure on the bubble.
    private void ApplyDispatchOutcome(OmnichannelMessage message, MessageDispatchResult dispatch, string conversationId)
    {
        var state = message.GetOrCreate<OutboundDeliveryState>();

        state.Attempts += 1;

        if (dispatch.Succeeded)
        {
            state.NextAttemptUtc = null;
            state.LastError = null;

            message.DeliveryStatus = MessageDeliveryStatus.Sent.ToString();
            message.ProviderMessageId = dispatch.ProviderMessageId;
            message.ErrorCode = null;
            message.Put(state);

            return;
        }

        state.LastError = dispatch.GetErrorText();

        if (OutboundDeliveryState.CanRetry(state.Attempts))
        {
            state.NextAttemptUtc = _clock.UtcNow.Add(OutboundDeliveryState.GetDelay(state.Attempts));

            message.DeliveryStatus = MessageDeliveryStatus.Queued.ToString();
        }
        else
        {
            state.NextAttemptUtc = null;

            message.DeliveryStatus = MessageDeliveryStatus.Failed.ToString();
        }

        message.ErrorCode = state.LastError;
        message.Put(state);

        _logger.LogWarning(
            "Outbound {Channel} dispatch attempt {Attempt} failed for conversation {ConversationId}: {Error}",
            message.Channel,
            state.Attempts,
            conversationId.SanitizeLogValue(),
            state.LastError);
    }
}

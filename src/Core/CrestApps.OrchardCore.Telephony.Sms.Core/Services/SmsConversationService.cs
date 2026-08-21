using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Models;
using CrestApps.OrchardCore.Telephony.Sms.Notifications;
using CrestApps.OrchardCore.Telephony.Sms.Services;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Sms;
using YesSql;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

/// <summary>
/// The default <see cref="ISmsConversationService"/>: authorizes and dispatches agent replies through the
/// per-number provider, and reconciles provider delivery receipts back onto the sent message.
/// </summary>
public sealed class SmsConversationService : ISmsConversationService
{
    private const int PreviewLength = 120;

    private readonly ISmsConversationStore _conversationStore;
    private readonly ISmsDispatcher _dispatcher;
    private readonly IContentManager _contentManager;
    private readonly ISmsRealTimeNotifier _notifier;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly Redactor _addressRedactor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsConversationService"/> class.
    /// </summary>
    public SmsConversationService(
        ISmsConversationStore conversationStore,
        ISmsDispatcher dispatcher,
        IContentManager contentManager,
        ISmsRealTimeNotifier notifier,
        ISession session,
        IClock clock,
        IRedactorProvider redactorProvider,
        ILogger<SmsConversationService> logger)
    {
        _conversationStore = conversationStore;
        _dispatcher = dispatcher;
        _contentManager = contentManager;
        _notifier = notifier;
        _session = session;
        _clock = clock;
        _addressRedactor = redactorProvider.GetRedactor(LogDataClassifications.AddressSet);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SmsSendResult> SendAsync(SmsSendRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Body) && (request.MediaUrls is null || request.MediaUrls.Count == 0))
        {
            return SmsSendResult.Failed("The message body is required.");
        }

        var conversation = await _conversationStore.FindByIdAsync(request.ConversationId, cancellationToken);

        if (conversation is null)
        {
            return SmsSendResult.Failed("The conversation was not found.");
        }

        if (!IsAuthorized(conversation, request.ActingAgentId))
        {
            return SmsSendResult.Failed("You are not allowed to send from this number.");
        }

        // Enforce the customer's SMS opt-out on every send.
        if (await IsOptedOutAsync(conversation, cancellationToken))
        {
            return SmsSendResult.Failed("The contact has opted out of SMS (Do not SMS).");
        }

        var message = new OmnichannelMessage
        {
            Id = UniqueId.GenerateId(),
            Channel = OmnichannelConstants.Channels.Sms,
            CustomerAddress = conversation.CustomerAddress,
            ServiceAddress = conversation.ServiceAddress,
            Content = request.Body,
            CreatedUtc = _clock.UtcNow,
            IsInbound = false,
            ConversationId = conversation.ItemId,
            SentByAgentId = request.ActingAgentId,
            DeliveryStatus = SmsDeliveryStatus.Queued.ToString(),
            MediaReferences = request.MediaUrls?.ToList() ?? [],
        };

        var dispatch = await _dispatcher.SendAsync(new SmsMessage
        {
            From = conversation.ServiceAddress,
            To = conversation.CustomerAddress,
            Body = request.Body,
        }, cancellationToken);

        if (dispatch.Succeeded)
        {
            message.DeliveryStatus = SmsDeliveryStatus.Sent.ToString();
        }
        else
        {
            message.DeliveryStatus = SmsDeliveryStatus.Failed.ToString();
            message.ErrorCode = string.Join("; ", dispatch.Errors.Select(e => e.Message.Value));

            _logger.LogWarning("Outbound SMS dispatch failed for conversation {ConversationId}: {Error}", conversation.ItemId.SanitizeLogValue(), message.ErrorCode);
        }

        await _session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);

        // Sending marks the thread read and, on an unassigned personal claim, assigns it to the acting agent.
        conversation.LastMessageUtc = message.CreatedUtc;
        conversation.LastMessagePreview = BuildPreview(request.Body);
        conversation.IsRead = true;
        conversation.UnreadCount = 0;
        conversation.ModifiedUtc = _clock.UtcNow;

        if (!string.IsNullOrEmpty(request.ActingAgentId) &&
            conversation.OwnerType == SmsConversationOwnerType.Personal &&
            conversation.AssignmentStatus == SmsConversationAssignmentStatus.Unassigned)
        {
            conversation.OwnerId = request.ActingAgentId;
            conversation.AssignedAgentId = request.ActingAgentId;
            conversation.AssignmentStatus = SmsConversationAssignmentStatus.Assigned;
        }

        await _conversationStore.UpdateAsync(conversation, cancellationToken);

        return new SmsSendResult
        {
            Succeeded = dispatch.Succeeded,
            Message = message,
            Error = dispatch.Succeeded ? null : message.ErrorCode,
        };
    }

    /// <inheritdoc/>
    public async Task<SmsSendResult> SendDirectAsync(string fromNumber, string toNumber, string body, string actingAgentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fromNumber) || string.IsNullOrWhiteSpace(toNumber))
        {
            return SmsSendResult.Failed("Both a sending number and a recipient are required.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return SmsSendResult.Failed("The message body is required.");
        }

        var serviceAddress = fromNumber.GetCleanedPhoneNumber();
        var customerAddress = toNumber.GetCleanedPhoneNumber();

        var conversation = await _conversationStore.FindByAddressesAsync(serviceAddress, customerAddress, cancellationToken);
        var isNew = conversation is null;

        if (isNew)
        {
            conversation = new SmsConversation
            {
                ItemId = UniqueId.GenerateId(),
                Channel = OmnichannelConstants.Channels.Sms,
                ServiceAddress = serviceAddress,
                CustomerAddress = customerAddress,
                Status = SmsConversationStatus.Open,
                OwnerType = SmsConversationOwnerType.Personal,
                OwnerId = actingAgentId,
                AssignedAgentId = actingAgentId,
                AssignmentStatus = string.IsNullOrEmpty(actingAgentId)
                    ? SmsConversationAssignmentStatus.Unassigned
                    : SmsConversationAssignmentStatus.Assigned,
                CreatedUtc = _clock.UtcNow,
            };
        }

        if (await IsOptedOutAsync(conversation, cancellationToken))
        {
            return SmsSendResult.Failed("The contact has opted out of SMS (Do not SMS).");
        }

        var message = new OmnichannelMessage
        {
            Id = UniqueId.GenerateId(),
            Channel = OmnichannelConstants.Channels.Sms,
            CustomerAddress = conversation.CustomerAddress,
            ServiceAddress = conversation.ServiceAddress,
            Content = body,
            CreatedUtc = _clock.UtcNow,
            IsInbound = false,
            ConversationId = conversation.ItemId,
            SentByAgentId = actingAgentId,
            DeliveryStatus = SmsDeliveryStatus.Queued.ToString(),
        };

        var dispatch = await _dispatcher.SendAsync(new SmsMessage
        {
            From = conversation.ServiceAddress,
            To = conversation.CustomerAddress,
            Body = body,
        }, cancellationToken);

        message.DeliveryStatus = dispatch.Succeeded ? SmsDeliveryStatus.Sent.ToString() : SmsDeliveryStatus.Failed.ToString();

        if (!dispatch.Succeeded)
        {
            message.ErrorCode = string.Join("; ", dispatch.Errors.Select(e => e.Message.Value));
        }

        await _session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);

        conversation.LastMessageUtc = message.CreatedUtc;
        conversation.LastMessagePreview = BuildPreview(body);
        conversation.IsRead = true;
        conversation.ModifiedUtc = _clock.UtcNow;

        if (isNew)
        {
            await _conversationStore.CreateAsync(conversation, cancellationToken);
        }
        else
        {
            await _conversationStore.UpdateAsync(conversation, cancellationToken);
        }

        return new SmsSendResult
        {
            Succeeded = dispatch.Succeeded,
            Message = message,
            Error = dispatch.Succeeded ? null : message.ErrorCode,
        };
    }

    /// <inheritdoc/>
    public async Task<bool> ApplyDeliveryReceiptAsync(SmsDeliveryReceipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var conversation = await _conversationStore.FindByAddressesAsync(
            receipt.ServiceAddress.GetCleanedPhoneNumber(),
            receipt.CustomerAddress.GetCleanedPhoneNumber(),
            cancellationToken);

        if (conversation is null)
        {
            return false;
        }

        var outbound = (await _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.ConversationId == conversation.ItemId && !index.IsInbound,
                collection: OmnichannelConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .ListAsync(cancellationToken))
            .ToArray();

        var message = !string.IsNullOrEmpty(receipt.ProviderMessageId)
            ? Array.Find(outbound, m => m.ProviderMessageId == receipt.ProviderMessageId) ?? outbound.FirstOrDefault(m => string.IsNullOrEmpty(m.ProviderMessageId))
            : outbound.FirstOrDefault();

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

        await _notifier.MessageDeliveryUpdatedAsync(new SmsDeliveryNotification
        {
            ConversationId = conversation.ItemId,
            MessageId = message.Id,
            Status = receipt.Status,
            ErrorCode = receipt.ErrorCode,
        }, cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public async Task<SmsSendResult> ClaimAsync(string conversationId, string actingAgentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(actingAgentId))
        {
            return SmsSendResult.Failed("An agent is required to claim a conversation.");
        }

        var conversation = await _conversationStore.FindByIdAsync(conversationId, cancellationToken);

        if (conversation is null)
        {
            return SmsSendResult.Failed("The conversation was not found.");
        }

        if (conversation.AssignmentStatus == SmsConversationAssignmentStatus.Assigned &&
            !string.IsNullOrEmpty(conversation.AssignedAgentId) &&
            conversation.AssignedAgentId != actingAgentId)
        {
            return SmsSendResult.Failed("The conversation has already been claimed by another agent.");
        }

        return await AssignInternalAsync(conversation, actingAgentId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SmsSendResult> AssignAsync(string conversationId, string targetAgentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(targetAgentId))
        {
            return SmsSendResult.Failed("A target agent is required.");
        }

        var conversation = await _conversationStore.FindByIdAsync(conversationId, cancellationToken);

        if (conversation is null)
        {
            return SmsSendResult.Failed("The conversation was not found.");
        }

        return await AssignInternalAsync(conversation, targetAgentId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SmsSendResult> SetStatusAsync(string conversationId, SmsConversationStatus status, CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationStore.FindByIdAsync(conversationId, cancellationToken);

        if (conversation is null)
        {
            return SmsSendResult.Failed("The conversation was not found.");
        }

        conversation.Status = status;
        conversation.ModifiedUtc = _clock.UtcNow;

        await _conversationStore.UpdateAsync(conversation, cancellationToken);

        return new SmsSendResult { Succeeded = true };
    }

    private async Task<SmsSendResult> AssignInternalAsync(SmsConversation conversation, string agentId, CancellationToken cancellationToken)
    {
        conversation.AssignedAgentId = agentId;
        conversation.AssignmentStatus = SmsConversationAssignmentStatus.Assigned;

        // A personal (non-queue) conversation follows its assignee as owner; a queue conversation keeps the
        // queue as its owner so it stays discoverable to the department.
        if (conversation.OwnerType == SmsConversationOwnerType.Personal)
        {
            conversation.OwnerId = agentId;
        }

        conversation.ModifiedUtc = _clock.UtcNow;

        await _conversationStore.UpdateAsync(conversation, cancellationToken);

        await _notifier.ConversationAssignedAsync(new SmsAssignmentNotification
        {
            ConversationId = conversation.ItemId,
            AssignedAgentId = conversation.AssignedAgentId,
            OwnerQueueId = conversation.OwnerType == SmsConversationOwnerType.Queue ? conversation.OwnerId : null,
        }, cancellationToken);

        return new SmsSendResult { Succeeded = true };
    }

    private static bool IsAuthorized(SmsConversation conversation, string actingAgentId)
    {
        // A queue (department) conversation is servable by queue members. Full queue-membership enforcement
        // lands with the queue-routed phase; phase 1 authorizes any signed-in agent for a shared-pool number.
        if (conversation.OwnerType == SmsConversationOwnerType.Queue)
        {
            return true;
        }

        // A personal conversation is servable by its owner or assignee, or claimable while still unassigned.
        if (conversation.AssignmentStatus == SmsConversationAssignmentStatus.Unassigned)
        {
            return true;
        }

        return !string.IsNullOrEmpty(actingAgentId) &&
            (actingAgentId == conversation.OwnerId || actingAgentId == conversation.AssignedAgentId);
    }

    private async Task<bool> IsOptedOutAsync(SmsConversation conversation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            return false;
        }

        var contact = await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);

        return contact is not null && contact.As<OmnichannelContactPart>()?.DoNotSms == true;
    }

    private static string BuildPreview(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        var normalized = content.ReplaceLineEndings(" ").Trim();

        return normalized.Length <= PreviewLength ? normalized : normalized[..PreviewLength];
    }
}

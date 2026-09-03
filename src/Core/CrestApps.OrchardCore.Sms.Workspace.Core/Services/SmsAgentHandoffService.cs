using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using CrestApps.OrchardCore.Sms.Workspace.Notifications;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

/// <summary>
/// The SMS implementation of <see cref="IOmnichannelHandoffService"/>. When an automated SMS conversation
/// escalates, it creates (or re-opens) the human <see cref="SmsConversation"/> owned by the target queue,
/// hydrates it with the prior automated transcript so the agent inherits the full context, and announces the
/// hand-off so the inbox lights up. From that point the existing SMS Workspace inbound routing keeps every
/// reply in the human thread.
/// </summary>
public sealed class SmsAgentHandoffService : IOmnichannelHandoffService
{
    private const int PreviewLength = 120;

    private readonly ISmsConversationStore _conversationStore;
    private readonly ISmsRealTimeNotifier _notifier;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsAgentHandoffService"/> class.
    /// </summary>
    public SmsAgentHandoffService(
        ISmsConversationStore conversationStore,
        ISmsRealTimeNotifier notifier,
        ISession session,
        IClock clock,
        ILogger<SmsAgentHandoffService> logger)
    {
        _conversationStore = conversationStore;
        _notifier = notifier;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool CanHandle(string channel)
        => string.Equals(channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<OmnichannelHandoffResult> RequestHandoffAsync(OmnichannelHandoffRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var activity = request.Activity;

        if (activity is null)
        {
            return OmnichannelHandoffResult.Failure("A handoff requires an activity.");
        }

        var queueId = string.IsNullOrWhiteSpace(request.TargetQueueId)
            ? null
            : request.TargetQueueId.Trim();

        if (string.IsNullOrEmpty(queueId))
        {
            return OmnichannelHandoffResult.Failure("A handoff requires a target queue.");
        }

        var serviceAddress = request.ServiceAddress.GetCleanedPhoneNumber();
        var contactAddress = request.ContactAddress.GetCleanedPhoneNumber();

        if (string.IsNullOrEmpty(serviceAddress) || string.IsNullOrEmpty(contactAddress))
        {
            return OmnichannelHandoffResult.Failure("A handoff requires both the service and contact addresses.");
        }

        var now = _clock.UtcNow;

        var conversation = await _conversationStore.FindByAddressesAsync(serviceAddress, contactAddress, cancellationToken);
        var isNew = conversation is null;

        if (isNew)
        {
            conversation = new SmsConversation
            {
                ItemId = UniqueId.GenerateId(),
                Channel = SmsWorkspaceConstants.Channel,
                ServiceAddress = serviceAddress,
                ContactAddress = contactAddress,
                ContactContentItemId = activity.ContactContentItemId,
                CreatedUtc = now,
            };
        }

        // Route the thread to the queue that handles escalations, unassigned so any eligible agent can claim it.
        conversation.OwnerType = SmsConversationOwnerType.Queue;
        conversation.OwnerId = queueId;
        conversation.AssignmentStatus = SmsConversationAssignmentStatus.Unassigned;
        conversation.AssignedAgentId = null;
        conversation.Status = SmsConversationStatus.Open;

        // Reserve the AI session so the thread can be traced back to the automated conversation it grew out of.
        conversation.AISessionId = activity.AISessionId;

        // The AI-written summary shown to the agent as warm context (falls back to the escalation reason).
        var summary = !string.IsNullOrWhiteSpace(request.Summary) ? request.Summary.Trim() : request.Reason?.Trim();

        if (!string.IsNullOrWhiteSpace(summary))
        {
            conversation.Summary = summary;
        }

        if (string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            conversation.ContactContentItemId = activity.ContactContentItemId;
        }

        // Hydrate the human thread with the prior automated transcript so the agent sees the actual conversation.
        var importedCount = await ImportTranscriptAsync(conversation, request.Transcript, serviceAddress, contactAddress, cancellationToken);

        var preview = BuildPreview(
            request.Transcript is { Count: > 0 }
                ? request.Transcript[^1].Content
                : request.Summary);

        conversation.LastMessageUtc = now;
        conversation.LastMessagePreview = preview;
        conversation.UnreadCount += Math.Max(1, importedCount);
        conversation.IsRead = false;
        conversation.ModifiedUtc = now;

        if (isNew)
        {
            await _conversationStore.CreateAsync(conversation, cancellationToken);
        }
        else
        {
            await _conversationStore.UpdateAsync(conversation, cancellationToken);
        }

        await _notifier.NewInboundMessageAsync(new SmsInboundNotification
        {
            ConversationId = conversation.ItemId,
            ServiceAddress = conversation.ServiceAddress,
            ContactAddress = conversation.ContactAddress,
            Preview = conversation.LastMessagePreview,
            UnreadCount = conversation.UnreadCount,
            ReceivedUtc = now,
            AssignedAgentId = conversation.AssignedAgentId,
            OwnerQueueId = queueId,
        }, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Handed off automated SMS Activity {ActivityId} to queue {QueueId} as conversation {ConversationId}.",
                activity.ItemId,
                queueId,
                conversation.ItemId);
        }

        return OmnichannelHandoffResult.Success("Handed off to the SMS queue.", conversation.ItemId);
    }

    private async Task<int> ImportTranscriptAsync(
        SmsConversation conversation,
        IReadOnlyList<OmnichannelHandoffMessage> transcript,
        string serviceAddress,
        string contactAddress,
        CancellationToken cancellationToken)
    {
        if (transcript is null || transcript.Count == 0)
        {
            return 0;
        }

        var count = 0;

        foreach (var entry in transcript)
        {
            if (string.IsNullOrWhiteSpace(entry.Content))
            {
                continue;
            }

            var message = new OmnichannelMessage
            {
                Id = UniqueId.GenerateId(),
                Channel = SmsWorkspaceConstants.Channel,
                CustomerAddress = contactAddress,
                ServiceAddress = serviceAddress,
                Content = entry.Content,
                IsInbound = entry.IsInbound,
                CreatedUtc = entry.CreatedUtc == default ? _clock.UtcNow : entry.CreatedUtc,
                ConversationId = conversation.ItemId,
            };

            await _session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
            count++;
        }

        return count;
    }

    private static string BuildPreview(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        var normalized = content.ReplaceLineEndings(" ").Trim();

        return normalized.Length <= PreviewLength
            ? normalized
            : normalized[..PreviewLength];
    }
}

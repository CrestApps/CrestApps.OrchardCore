using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Notifications;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Default <see cref="ISmsFirstResponseSlaService"/>. The deadline lives on the conversation so the same record
/// the inbox renders is the one the sweep reads, and a breach is announced exactly once.
/// </summary>
public sealed class SmsFirstResponseSlaService : ISmsFirstResponseSlaService
{
    private readonly ISmsConversationStore _conversationStore;
    private readonly ISmsQueuePolicyReader _queuePolicyReader;
    private readonly ISmsRealTimeNotifier _notifier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsFirstResponseSlaService"/> class.
    /// </summary>
    public SmsFirstResponseSlaService(
        ISmsConversationStore conversationStore,
        ISmsQueuePolicyReader queuePolicyReader,
        ISmsRealTimeNotifier notifier,
        TimeProvider timeProvider,
        ILogger<SmsFirstResponseSlaService> logger)
    {
        _conversationStore = conversationStore;
        _queuePolicyReader = queuePolicyReader;
        _notifier = notifier;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ApplyFirstResponseTargetAsync(SmsConversation conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        if (conversation.FirstResponseDueUtc is not null ||
            conversation.FirstRespondedUtc is not null ||
            string.IsNullOrEmpty(conversation.OwnerId))
        {
            return;
        }

        var policy = await _queuePolicyReader.ReadAsync(conversation.OwnerId, cancellationToken);

        if (!policy.Exists || policy.FirstResponseTargetSeconds <= 0)
        {
            return;
        }

        conversation.FirstResponseDueUtc = _timeProvider.GetUtcNow().UtcDateTime.AddSeconds(policy.FirstResponseTargetSeconds);
    }

    /// <inheritdoc/>
    public void MarkResponded(SmsConversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        conversation.FirstRespondedUtc ??= _timeProvider.GetUtcNow().UtcDateTime;
        conversation.FirstResponseDueUtc = null;
    }

    /// <inheritdoc/>
    public async Task<int> EscalateOverdueAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var overdue = await _conversationStore.GetFirstResponseOverdueAsync(now, cancellationToken);

        if (overdue.Count == 0)
        {
            return 0;
        }

        var escalated = 0;

        foreach (var conversation in overdue)
        {
            // Announced once. The sweep runs every thirty seconds, and re-announcing the same breach every pass
            // turns a signal that a customer is waiting into noise a supervisor learns to ignore.
            if (conversation.FirstResponseBreached)
            {
                continue;
            }

            conversation.FirstResponseBreached = true;
            conversation.ModifiedUtc = now;

            await _conversationStore.UpdateAsync(conversation, cancellationToken);

            await _notifier.FirstResponseBreachedAsync(
                new SmsFirstResponseBreachNotification
                {
                    ConversationId = conversation.ItemId,
                    OwnerQueueId = conversation.OwnerId,
                    AssignedAgentId = conversation.AssignedAgentId,
                    DueUtc = conversation.FirstResponseDueUtc ?? now,
                    BreachedUtc = now,
                },
                cancellationToken);

            escalated++;
        }

        if (escalated > 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Announced {Count} SMS first-response breach(es).", escalated);
        }

        return escalated;
    }
}

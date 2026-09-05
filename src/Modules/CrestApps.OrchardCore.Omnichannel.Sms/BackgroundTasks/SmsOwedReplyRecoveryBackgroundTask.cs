using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Handlers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.BackgroundTasks;

/// <summary>
/// Recovers automated SMS conversations whose reply was owed but never sent.
/// </summary>
/// <remarks>
/// The inbound webhook acknowledges Twilio immediately and generates the reply on a background scope, and the
/// single-active-generation registry that serializes those replies lives in memory. Without a distributed backing
/// (for example Redis) that registry is per-node and does not survive a restart, so a reply that was mid-flight when
/// the process stopped is simply lost: the customer's message sits in the transcript unanswered while the activity
/// waits in <see cref="ActivityStatus.AwaitingCustomerAnswer"/>. The no-response timeout would eventually mark such a
/// conversation <see cref="ActivityStatus.Failed"/> as if the customer went quiet, which is wrong — it was our reply
/// that was dropped. This task periodically finds those conversations (a trailing customer message with no reply after
/// it) and re-drives the handler, which is idempotent: the message is not stored twice, and if a live generation has
/// meanwhile answered, the owed-reply gate simply sends nothing.
/// </remarks>
[BackgroundTask(
    Title = "Automated SMS Owed-Reply Recovery",
    Schedule = "*/10 * * * *",
    Description = "Re-drives automated SMS conversations whose AI reply was lost before it was sent.",
    LockTimeout = 5_000,
    LockExpiration = _leaseMilliseconds)]
public sealed class SmsOwedReplyRecoveryBackgroundTask : IBackgroundTask
{
    private const int _leaseMilliseconds = 300_000;
    private const int _batchSize = 100;
    private const int _maxConversationsPerInvocation = 200;

    // A reply is only recovered when the customer's unanswered message is recent. This is the whole point of the
    // staleness bound: recovery exists to answer a reply that was mid-flight when the process stopped (picked up on
    // the next scheduled run, minutes later), NOT to resurrect a thread the customer sent to hours or days ago. Waking
    // an old, wound-down conversation with a late reply is worse than staying silent — the customer has moved on, and
    // the no-response timeout already governs those. The window comfortably covers a normal restart and a couple of
    // missed runs, while excluding anything genuinely stale.
    private const int _maxOwedReplyAgeMinutes = 30;

    /// <summary>
    /// Asynchronously performs the do work operation.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<SmsOwedReplyRecoveryBackgroundTask>>();

        var session = serviceProvider.GetRequiredService<ISession>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var promptStore = serviceProvider.GetRequiredService<IAIChatSessionPromptStore>();
        var chatSessionManager = serviceProvider.GetRequiredService<IAIChatSessionManager>();
        var endpointCatalog = serviceProvider.GetRequiredService<ICatalog<OmnichannelChannelEndpoint>>();

        // Re-drive ONLY the automated AI handler. Invoking every IOmnichannelEventHandler would also hand the event to
        // the human SMS-portal inbound processor, which would try to route it as a workspace conversation — a message
        // that belongs to an automated activity is not a portal message.
        var handler = serviceProvider.GetServices<IOmnichannelEventHandler>()
            .OfType<SmsOmnichannelEventHandler>()
            .FirstOrDefault();

        if (handler is null)
        {
            return;
        }

        // Stop before the lock lease can expire; the remainder is picked up on the next scheduled run. Each recovered
        // conversation runs a full reply generation (settle + AI + "typing"), so the budget is charged per item.
        var deadline = clock.UtcNow.AddMilliseconds(_leaseMilliseconds * 0.6);

        // Only conversations whose unanswered customer message arrived after this moment are eligible.
        var owedReplyCutoff = clock.UtcNow.AddMinutes(-_maxOwedReplyAgeMinutes);

        long documentId = 0;
        var processedCount = 0;

        while (processedCount < _maxConversationsPerInvocation && clock.UtcNow < deadline)
        {
            var activities = await session.Query<OmnichannelActivity, OmnichannelActivityIndex>(x =>
                    x.Status == ActivityStatus.AwaitingCustomerAnswer &&
                    x.InteractionType == ActivityInteractionType.Automated &&
                    x.Channel == OmnichannelConstants.Channels.Sms &&
                    x.DocumentId > documentId,
                    collection: OmnichannelConstants.CollectionName)
                .OrderBy(x => x.DocumentId)
                .Take(_batchSize)
                .ListAsync(cancellationToken);

            if (!activities.Any())
            {
                break;
            }

            foreach (var activity in activities)
            {
                if (clock.UtcNow >= deadline)
                {
                    return;
                }

                documentId = activity.Id;
                processedCount++;

                try
                {
                    await TryRecoverAsync(activity, chatSessionManager, promptStore, endpointCatalog, handler, owedReplyCutoff, logger, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to recover the owed automated SMS reply for Activity {ActivityId}.", activity.ItemId.SanitizeLogValue());
                }
            }
        }
    }

    private static async Task TryRecoverAsync(
        OmnichannelActivity activity,
        IAIChatSessionManager chatSessionManager,
        IAIChatSessionPromptStore promptStore,
        ICatalog<OmnichannelChannelEndpoint> endpointCatalog,
        SmsOmnichannelEventHandler handler,
        DateTime owedReplyCutoff,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            return;
        }

        // A live inbound is already composing a reply for this conversation on this node; leave it alone. After a
        // restart the registry is empty, so genuinely stranded conversations are not skipped here.
        if (SmsOmnichannelEventHandler.IsGenerating(activity.AISessionId))
        {
            return;
        }

        var chatSession = await chatSessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);

        if (chatSession is null)
        {
            return;
        }

        var prompts = (await promptStore.GetPromptsAsync(chatSession.SessionId))
            .Where(x => !x.IsGeneratedPrompt)
            .ToList();

        // A reply is owed only when the last thing said was the customer's. If the last message is the assistant's,
        // the conversation was already answered and is legitimately waiting on the customer.
        var lastPrompt = prompts.LastOrDefault();

        if (lastPrompt is null || lastPrompt.Role != ChatRole.User || string.IsNullOrWhiteSpace(lastPrompt.Content))
        {
            return;
        }

        // Only answer a recently stranded message. An owed reply lost to a restart is recovered on the next scheduled
        // run (minutes later); a much older unanswered message belongs to a wound-down conversation that should not be
        // resurrected with a late reply. Prompts written before this task stamped CreatedUtc read as the default value,
        // which is safely treated as too old.
        if (lastPrompt.CreatedUtc < owedReplyCutoff)
        {
            return;
        }

        if (string.IsNullOrEmpty(activity.ChannelEndpointId))
        {
            return;
        }

        var endpoint = await endpointCatalog.FindByIdAsync(activity.ChannelEndpointId, cancellationToken);

        if (endpoint is null ||
            !string.Equals(endpoint.Channel, activity.Channel, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(endpoint.Value))
        {
            return;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Re-driving an owed automated SMS reply for Activity {ActivityId}.", activity.ItemId.SanitizeLogValue());
        }

        // Re-raise the customer's trailing message as an inbound event. The handler's idempotent store recognises the
        // message is already the trailing turn and does not duplicate it, then generates and sends the single owed
        // reply through the same per-conversation lock and generation registry as a live delivery.
        var omnichannelEvent = new OmnichannelEvent
        {
            EventType = OmnichannelConstants.Events.SmsReceived,
            Subject = $"SMS from {activity.PreferredDestination}",
            Data = BinaryData.FromString(lastPrompt.Content),
            Message = new OmnichannelMessage
            {
                Channel = OmnichannelConstants.Channels.Sms,
                CustomerAddress = activity.PreferredDestination,
                ServiceAddress = endpoint.Value,
                Content = lastPrompt.Content,
                IsInbound = true,
            },
        };

        await handler.HandleAsync(omnichannelEvent, cancellationToken);
    }
}

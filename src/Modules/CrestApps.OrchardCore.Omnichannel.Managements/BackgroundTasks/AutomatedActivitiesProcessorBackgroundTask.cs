using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using CrestApps.Core.Support;
using OrchardCore.ContentManagement;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;

[BackgroundTask(
    Title = "Omnichannel Automated Activities Processor",
    Schedule = "*/5 * * * *",
    Description = "Processes omnichannel activities.",
    LockTimeout = 5_000,
    LockExpiration = _leaseMilliseconds)]

/// <summary>
/// Represents the automated activities processor background task.
/// </summary>
public sealed class AutomatedActivitiesProcessorBackgroundTask : IBackgroundTask
{
    private const int _leaseMilliseconds = 600_000;
    // The lease is a compile-time attribute argument, so it stays a constant; everything else the pass does is
    // read from options at run time.


    /// <summary>
    /// Asynchronously performs the do work operation.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var options = serviceProvider.GetRequiredService<IOptions<OmnichannelAutomationOptions>>().Value;
        var batchSize = options.ProcessorBatchSize;
        var maxActivitiesPerInvocation = options.MaxActivitiesPerInvocation;
        var maxAttempts = options.MaxProcessingAttempts;
        var retryDelayMinutes = options.RetryDelayMinutes;

        var processors = serviceProvider.GetService<IEnumerable<IOmnichannelProcessor>>()
            .ToDictionary(x => x.Channel, StringComparer.OrdinalIgnoreCase);

        var logger = serviceProvider.GetRequiredService<ILogger<AutomatedActivitiesProcessorBackgroundTask>>();

        if (processors.Count == 0)
        {
            logger.LogWarning("No omnichannel processors were found. Make sure at least one processor is registered.");

            return;
        }

        var session = serviceProvider.GetRequiredService<ISession>();
        var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Stop the run comfortably before the distributed-lock lease can expire. OrchardCore does not cancel a task
        // when its lease elapses, so a run that outlived the lease while still alive could otherwise keep sending
        // after another node had taken over. Stopping at a fraction of the lease keeps this node from handing an
        // uncommitted backlog to a peer; the remaining work is picked up on the next scheduled invocation. The
        // budget is charged against both the expiry pass and the send loop, and is re-checked per item, so a single
        // slow send or expiry cannot overrun it by more than one item.
        var deadline = now.AddMilliseconds(_leaseMilliseconds * 0.6);

        await ExpireNoResponseActivitiesAsync(serviceProvider, session, timeProvider, now, deadline, batchSize, maxActivitiesPerInvocation, logger, cancellationToken);

        // Commit the expiry pass on its own so its changes are durable regardless of what the processing loop does.
        await session.SaveChangesAsync(cancellationToken);

        long documentId = 0;
        var processedCount = 0;

        while (processedCount < maxActivitiesPerInvocation && timeProvider.GetUtcNow().UtcDateTime < deadline)
        {
            // Keyset pagination on the monotonically increasing document id. Combining an OFFSET skip with this
            // cursor (as an earlier revision did) advanced the window twice per batch and silently skipped every
            // other page of due activities.
            var activities = await session.Query<OmnichannelActivity, OmnichannelActivityIndex>(x =>
                    (x.Status == ActivityStatus.NotStated || x.Status == ActivityStatus.Scheduled) &&
                    x.InteractionType == ActivityInteractionType.Automated &&
                    x.ScheduledUtc <= now &&
                    x.Channel.IsIn(processors.Keys) &&
                    x.DocumentId > documentId,
                collection: OmnichannelConstants.CollectionName)
                .OrderBy(x => x.DocumentId)
                .Take(batchSize)
                .ListAsync(cancellationToken);

            if (!activities.Any())
            {
                break;
            }

            foreach (var activity in activities)
            {
                // Enforce the wall-clock budget per item, not just per batch. A batch that starts just under the
                // deadline must not run a full page of additional sends past it, or the run could outlive its lease.
                if (timeProvider.GetUtcNow().UtcDateTime >= deadline)
                {
                    break;
                }

                documentId = activity.Id;

                try
                {
                    var processor = processors[activity.Channel];

                    // Asked again here, and not only when the batch was loaded. A batch is loaded once and its
                    // activities come due later -- often hours later, and later still if they are rescheduled --
                    // so the preference read at load time is a statement about the past. Somebody who asks to be
                    // left alone in the meantime is still holding an activity with their number on it, and
                    // nothing between this line and the provider looks at the contact again.
                    var refusal = await ScreenAsync(serviceProvider, activity, cancellationToken);

                    if (refusal is not null)
                    {
                        activity.Status = ActivityStatus.Cancelled;
                        activity.CompletedUtc ??= now;
                        activity.Notes = string.IsNullOrWhiteSpace(activity.Notes)
                            ? refusal
                            : activity.Notes + Environment.NewLine + refusal;

                        await session.SaveAsync(activity, false, collection: OmnichannelConstants.CollectionName, cancellationToken);

                        if (logger.IsEnabled(LogLevel.Information))
                        {
                            logger.LogInformation(
                                "Activity '{ActivityId}' was cancelled instead of started on '{Channel}': {Reason}",
                                activity.ItemId.SanitizeLogValue(),
                                activity.Channel.SanitizeLogValue(),
                                refusal.SanitizeLogValue());
                        }

                        continue;
                    }

                    await processor.StartAsync(activity, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "An error occurred while processing the activity with id '{ActivityId}'", activity.ItemId);

                    // Persist a failure transition so the activity leaves the due set. Without it a permanently
                    // failing activity (for example a misconfigured automated inventory load) would re-match the due
                    // query on every invocation and consume one of the bounded per-invocation slots forever,
                    // eventually starving all healthy outbound work. ProcessingAttempts is an internal counter that
                    // starts at zero and is never projected from the contact-center work state, so this transition
                    // cannot be reset (and cannot corrupt the routing-owned Attempts field or its reports).
                    activity.ProcessingAttempts++;

                    if (activity.ProcessingAttempts >= maxAttempts)
                    {
                        activity.Status = ActivityStatus.Failed;

                        if (string.IsNullOrWhiteSpace(activity.Notes))
                        {
                            activity.Notes = "The automated activity failed after exhausting the maximum number of processing attempts.";
                        }
                    }
                    else
                    {
                        activity.ScheduledUtc = now.AddMinutes(retryDelayMinutes * activity.ProcessingAttempts);
                    }
                }

                await session.SaveAsync(activity, false, collection: OmnichannelConstants.CollectionName, cancellationToken);
                processedCount++;
            }

            // Commit each batch so processed activities are durably marked before the next batch is sent. These
            // sends are not individually idempotent, so the commit boundary — together with the per-item wall-clock
            // budget above that stops the run before its lease can expire — keeps a still-running node from handing
            // an uncommitted backlog to a peer. If this node is instead killed mid-batch, only the single uncommitted
            // in-flight batch (at most batchSize) is re-sent by the node that acquires the lock next, rather than
            // the whole backlog.
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task ExpireNoResponseActivitiesAsync(
        IServiceProvider serviceProvider,
        ISession session,
        TimeProvider timeProvider,
        DateTime now,
        DateTime deadline,
        int batchSize,
        int maxActivitiesPerInvocation,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var subjectFlowSettingsService = serviceProvider.GetRequiredService<ISubjectFlowSettingsService>();

        var configuredFlowSettings = await subjectFlowSettingsService.GetConfiguredFlowSettingsAsync(cancellationToken);

        // Only subjects whose flow defines a no-response timeout can ever expire here. Restricting the query to those
        // subject types keeps no-timeout conversations (which this pass never transitions) out of the candidate set
        // entirely, so they can neither occupy the head of the query and starve activities that can actually expire
        // nor have their user-visible ScheduledUtc rewritten with a sentinel to force them out.
        var timeoutSubjectTypes = configuredFlowSettings
            .Where(OmnichannelAutomationHelper.HasNoResponseTimeout)
            .Select(settings => settings.SubjectContentType)
            .Where(subjectContentType => !string.IsNullOrEmpty(subjectContentType))
            .ToArray();

        if (timeoutSubjectTypes.Length == 0)
        {
            return;
        }

        long documentId = 0;
        var processedCount = 0;

        while (processedCount < maxActivitiesPerInvocation && timeProvider.GetUtcNow().UtcDateTime < deadline)
        {
            // Keyset pagination so a large expiry backlog drains over successive batches without an OFFSET skip.
            var expiredActivities = await session.Query<OmnichannelActivity, OmnichannelActivityIndex>(x =>
                    x.Status == ActivityStatus.AwaitingCustomerAnswer &&
                    x.InteractionType == ActivityInteractionType.Automated &&
                    x.ScheduledUtc <= now &&
                    x.SubjectContentType.IsIn(timeoutSubjectTypes) &&
                    x.DocumentId > documentId,
                    collection: OmnichannelConstants.CollectionName)
                .OrderBy(x => x.DocumentId)
                .Take(batchSize)
                .ListAsync(cancellationToken);

            if (!expiredActivities.Any())
            {
                break;
            }

            foreach (var activity in expiredActivities)
            {
                // Share the run's wall-clock budget with the send loop so the expiry pass cannot consume it all.
                if (timeProvider.GetUtcNow().UtcDateTime >= deadline)
                {
                    break;
                }

                documentId = activity.Id;
                processedCount++;

                activity.Status = ActivityStatus.Failed;

                if (string.IsNullOrWhiteSpace(activity.Notes))
                {
                    activity.Notes = "The automated SMS activity failed because the contact stopped responding.";
                }

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Automated activity '{ActivityId}' failed because the contact did not respond before the configured timeout.", activity.ItemId);
                }

                await session.SaveAsync(activity, false, collection: OmnichannelConstants.CollectionName, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Whether the contact this activity is for has since asked not to be reached on its channel.
    /// </summary>
    /// <remarks>
    /// An activity with no contact is nobody's preference to state, so it proceeds. A contact that can no longer
    /// be found proceeds too: a deleted record is not a request to be left alone, and the activity still carries
    /// the destination it was loaded with.
    /// </remarks>
    /// <summary>
    /// The reason this activity may not be placed, or <see langword="null"/> when it may.
    /// </summary>
    /// <remarks>
    /// Two questions, asked in the cheap order. Whether the person has asked to be left alone is answered from
    /// the contact we already have to load; whether anything else forbids the call -- a national registry, the
    /// hour in their region -- is answered by whichever screeners the tenant has registered, and may cost a
    /// network round trip.
    /// </remarks>
    private static async Task<string> ScreenAsync(
        IServiceProvider serviceProvider,
        OmnichannelActivity activity,
        CancellationToken cancellationToken)
    {
        if (await HasOptedOutAsync(serviceProvider, activity, cancellationToken))
        {
            return "The contact has asked not to be reached on this channel.";
        }

        var screeners = serviceProvider.GetServices<IAutomatedActivityScreener>()
            .Where(screener => string.Equals(screener.Channel, activity.Channel, StringComparison.OrdinalIgnoreCase));

        foreach (var screener in screeners)
        {
            var result = await screener.ScreenAsync(activity, cancellationToken);

            if (!result.IsAllowed)
            {
                // The first refusal settles it. Asking the rest would not change the outcome and each one may
                // cost a call to somebody else's service.
                return string.IsNullOrWhiteSpace(result.Description)
                    ? result.Reason ?? "The call was refused by screening."
                    : result.Description;
            }
        }

        return null;
    }

    private static async Task<bool> HasOptedOutAsync(
        IServiceProvider serviceProvider,
        OmnichannelActivity activity,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(activity.ContactContentItemId))
        {
            return false;
        }

        var contentManager = serviceProvider.GetRequiredService<IContentManager>();

        // Latest rather than published: an opt-out recorded but not yet published is still an opt-out, and the
        // question here is whether anybody has told us to stop -- not whether the record has been approved.
        var contact = await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

        return OmnichannelHelper.HasOptedOut(contact, activity.Channel);
    }
}

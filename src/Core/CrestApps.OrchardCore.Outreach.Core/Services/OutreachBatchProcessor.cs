using CrestApps.Core.Support;
using CrestApps.OrchardCore.Outreach.Core.Models;
using CrestApps.OrchardCore.Outreach.Models;
using CrestApps.OrchardCore.WorkQueues;
using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Outreach.Core.Services;

/// <summary>
/// Sends outreach batches. Each due batch is claimed on the leased work queue and processed in an isolated scope: it
/// reads one page of its run's audience, skips recipients a policy rules out, renders and sends to the rest, and saves
/// its progress after every recipient, so a retry never sends twice to the same one. When the batch is done its outcome
/// is added to the run and the next page is queued, in the same transaction that completes the batch.
/// </summary>
public sealed class OutreachBatchProcessor
{
    /// <summary>
    /// How long a worker owns a batch before another may claim it.
    /// </summary>
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    private static readonly Func<int, TimeSpan> _backoff = LeasedWorkBackoff.Exponential(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(30));
    private static readonly TimeSpan _unavailableDelay = TimeSpan.FromMinutes(5);

    private readonly IOutreachBatchStore _batchStore;
    private readonly IOutreachRunStore _runStore;
    private readonly CatalogLeasedWorkQueue<OutreachBatch> _queue;
    private readonly IEnumerable<IOutreachAudienceSource> _sources;
    private readonly IEnumerable<IOutreachChannelSender> _senders;
    private readonly IEnumerable<IOutreachTemplateRenderer> _renderers;
    private readonly IEnumerable<IOutreachRecipientPolicy> _policies;
    private readonly IEnumerable<IOutreachRunObserver> _observers;
    private readonly OutreachQuietHours _quietHours;
    private readonly IOutreachScopeExecutor _scopeExecutor;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutreachBatchProcessor"/> class.
    /// </summary>
    /// <param name="batchStore">The batch store.</param>
    /// <param name="runStore">The run store.</param>
    /// <param name="sources">The audience sources.</param>
    /// <param name="senders">The channel senders.</param>
    /// <param name="renderers">The template renderers.</param>
    /// <param name="policies">The recipient policies.</param>
    /// <param name="observers">The run observers.</param>
    /// <param name="quietHours">Works out when a run in quiet hours may send again.</param>
    /// <param name="scopeExecutor">Runs each batch in an isolated scope.</param>
    /// <param name="session">The session of this scope, which the batch's progress is committed in.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public OutreachBatchProcessor(
        IOutreachBatchStore batchStore,
        IOutreachRunStore runStore,
        IEnumerable<IOutreachAudienceSource> sources,
        IEnumerable<IOutreachChannelSender> senders,
        IEnumerable<IOutreachTemplateRenderer> renderers,
        IEnumerable<IOutreachRecipientPolicy> policies,
        IEnumerable<IOutreachRunObserver> observers,
        OutreachQuietHours quietHours,
        IOutreachScopeExecutor scopeExecutor,
        ISession session,
        IClock clock,
        ILogger<OutreachBatchProcessor> logger)
    {
        _batchStore = batchStore;
        _runStore = runStore;
        _queue = OutreachBatchQueue.Create(batchStore, session, clock);
        _sources = sources;
        _senders = senders;
        _renderers = renderers;
        _policies = policies;
        _observers = observers;
        _quietHours = quietHours;
        _scopeExecutor = scopeExecutor;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Processes due batches, each in its own isolated scope, until none is due or the budget is spent.
    /// </summary>
    /// <param name="maxBatches">The most batches to process.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many batches were attempted.</returns>
    public async Task<int> ProcessDueAsync(int maxBatches, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBatches);

        var attempted = 0;

        while (attempted < maxBatches)
        {
            var due = await _batchStore.GetDueAsync(_clock.UtcNow, Math.Min(25, maxBatches - attempted), cancellationToken);

            if (due.Count == 0)
            {
                break;
            }

            foreach (var batchId in due.Select(batch => batch.ItemId).ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempted++;

                try
                {
                    await _scopeExecutor.ExecuteAsync(services =>
                        services.GetRequiredService<OutreachBatchProcessor>().ProcessBatchAsync(batchId, cancellationToken));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // The batch's isolated scope failed, for example because another node took it over after its lease
                    // expired. The batch is claimed again in a fresh scope later; carry on with the rest.
                    _logger.LogWarning(
                        "Outreach batch '{BatchId}' failed with {ExceptionType}; it is retried later.",
                        batchId.SanitizeLogValue(),
                        exception.GetType().Name);
                }
            }
        }

        return attempted;
    }

    /// <summary>
    /// Claims one batch when it is due and sends it. Call it in an isolated scope: a lost race surfaces as the
    /// <see cref="ConcurrencyException"/> of this scope's session and is left to propagate.
    /// </summary>
    /// <param name="batchId">The batch identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What became of the batch.</returns>
    public async Task<OutreachBatchOutcome> ProcessBatchAsync(string batchId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(batchId);

        var batch = await _queue.TryClaimAsync(batchId, Lease, cancellationToken);

        if (batch is null)
        {
            return OutreachBatchOutcome.NotDue;
        }

        var run = await _runStore.FindByIdAsync(batch.RunId, cancellationToken);

        if (run is null)
        {
            await _queue.AbandonAsync(batch, "The outreach run no longer exists.", cancellationToken: cancellationToken);

            return OutreachBatchOutcome.Abandoned;
        }

        if (run.IsFinished)
        {
            await _queue.CompleteAsync(batch, cancellationToken: cancellationToken);

            return OutreachBatchOutcome.Completed;
        }

        var source = _sources.FirstOrDefault(candidate => string.Equals(candidate.Name, run.AudienceSource, StringComparison.Ordinal));
        var sender = _senders.FirstOrDefault(candidate => string.Equals(candidate.Name, run.Sender, StringComparison.Ordinal));
        var rendererName = string.IsNullOrEmpty(run.Renderer) ? OutreachConstants.TextRenderer : run.Renderer;
        var renderer = _renderers.FirstOrDefault(candidate => string.Equals(candidate.Name, rendererName, StringComparison.Ordinal));

        if (source is null || sender is null || renderer is null)
        {
            // The feature that provides it may be turned off for now. Waiting does not use up an attempt.
            await _queue.RetryAsync(batch, "The run's audience source, sender or renderer is not available.", _ => _unavailableDelay, countAttempt: false, cancellationToken: cancellationToken);

            _logger.LogWarning(
                "Outreach run '{RunId}' waits: its audience source '{Source}', sender '{Sender}' or renderer '{Renderer}' is not available.",
                run.ItemId.SanitizeLogValue(),
                run.AudienceSource.SanitizeLogValue(),
                run.Sender.SanitizeLogValue(),
                rendererName.SanitizeLogValue());

            return OutreachBatchOutcome.Deferred;
        }

        var now = _clock.UtcNow;

        if (sender.ObservesQuietHours(run) &&
            await _quietHours.GetResumeTimeAsync(run, now, cancellationToken) is { } resumeUtc)
        {
            await _queue.RetryAsync(batch, "QuietHours", _ => resumeUtc - now, countAttempt: false, cancellationToken: cancellationToken);

            return OutreachBatchOutcome.Deferred;
        }

        OutreachAudiencePage page;

        try
        {
            page = await source.GetPageAsync(run, batch.Cursor, run.PageSize, cancellationToken) ?? OutreachAudiencePage.Empty;

            var processedKeys = batch.ProcessedKeys.ToHashSet(StringComparer.Ordinal);

            foreach (var recipient in page.Recipients)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var key = recipient.Key ?? recipient.Address ?? string.Empty;

                if (!processedKeys.Add(key))
                {
                    continue;
                }

                switch (await HandleRecipientAsync(run, recipient, sender, renderer, cancellationToken))
                {
                    case RecipientOutcome.Sent:
                        batch.Sent++;
                        break;
                    case RecipientOutcome.Skipped:
                        batch.Skipped++;
                        break;
                    default:
                        batch.Failed++;
                        break;
                }

                batch.ProcessedKeys.Add(key);

                // Saved after every recipient, together with what the sender stored, so a batch that stops here is
                // picked up after this recipient.
                await SaveProgressAsync(batch, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ConcurrencyException)
        {
            // Another worker took the batch over after this one's lease expired, or the run changed. This session is
            // spent; the batch is claimed again in a fresh scope.
            throw;
        }
        catch (Exception exception)
        {
            return await FailAsync(batch, run, exception, cancellationToken);
        }

        // A page without a next cursor is the last; one that does not move the cursor is treated as the last too, so a
        // source that keeps returning the same page cannot loop forever.
        var nextCursor = string.Equals(page.NextCursor, batch.Cursor, StringComparison.Ordinal)
            ? null
            : page.NextCursor;

        // A commit forgets the documents the session read before it (the claim, a sender's own save, the progress), and
        // saving one of them afterwards would insert a copy. The run is read again here, after the last commit.
        run = await _runStore.FindByIdAsync(batch.RunId, cancellationToken);

        if (run is null || run.IsFinished)
        {
            await _queue.CompleteAsync(batch, item => CopyProgress(batch, item), cancellationToken);

            return OutreachBatchOutcome.Completed;
        }

        if (run.BatchesCompleted < batch.Sequence)
        {
            run.Sent += batch.Sent;
            run.Skipped += batch.Skipped;
            run.Failed += batch.Failed;
            run.BatchesCompleted = batch.Sequence;
            run.Cursor = nextCursor;
        }

        run.Status = nextCursor is null ? OutreachRunStatus.Completed : OutreachRunStatus.Running;
        run.StartedUtc ??= now;
        run.ModifiedUtc = _clock.UtcNow;

        if (nextCursor is null)
        {
            run.CompletedUtc = run.ModifiedUtc;
        }
        else
        {
            await _queue.EnqueueAsync(new OutreachBatch
            {
                RunId = run.ItemId,
                Sequence = batch.Sequence + 1,
                Cursor = nextCursor,
                CreatedUtc = run.ModifiedUtc.Value,
            }, cancellationToken);
        }

        await _runStore.UpdateAsync(run, cancellationToken);
        await NotifyAsync(run, cancellationToken);

        // Completing the batch commits the run's progress, the next batch and the observers' changes with it.
        await _queue.CompleteAsync(batch, item =>
        {
            CopyProgress(batch, item);
            item.NextCursor = nextCursor;
        }, cancellationToken);

        return OutreachBatchOutcome.Completed;
    }

    private async Task SaveProgressAsync(OutreachBatch claim, CancellationToken cancellationToken)
    {
        // Read the batch again: a commit since it was read (by the claim, a sender or the last recipient's progress)
        // detached the earlier copy. Saving it only while this worker still owns the claim fences out a late worker.
        var stored = await _batchStore.FindByIdAsync(claim.ItemId, cancellationToken);

        if (stored is null ||
            stored.Status != LeasedWorkItemStatus.Claimed ||
            !string.Equals(stored.OwnerToken, claim.OwnerToken, StringComparison.Ordinal) ||
            stored.FenceToken != claim.FenceToken)
        {
            throw new ConcurrencyException(new Document());
        }

        CopyProgress(claim, stored);

        await _batchStore.UpdateAsync(stored, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }

    private static void CopyProgress(OutreachBatch from, OutreachBatch to)
    {
        if (ReferenceEquals(from, to))
        {
            return;
        }

        to.ProcessedKeys = [.. from.ProcessedKeys];
        to.Sent = from.Sent;
        to.Skipped = from.Skipped;
        to.Failed = from.Failed;
    }

    private async Task<RecipientOutcome> HandleRecipientAsync(
        OutreachRun run,
        OutreachRecipient recipient,
        IOutreachChannelSender sender,
        IOutreachTemplateRenderer renderer,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(recipient.SkipReason) || string.IsNullOrWhiteSpace(recipient.Address))
        {
            return RecipientOutcome.Skipped;
        }

        foreach (var policy in _policies)
        {
            if (!string.IsNullOrWhiteSpace(await policy.GetSkipReasonAsync(run, recipient, cancellationToken)))
            {
                return RecipientOutcome.Skipped;
            }
        }

        var content = await renderer.RenderAsync(run, recipient, cancellationToken);
        var result = await sender.SendAsync(run, recipient, content, cancellationToken);

        if (result.Succeeded)
        {
            return RecipientOutcome.Sent;
        }

        _logger.LogWarning(
            "Outreach run '{RunId}' could not send to recipient '{RecipientKey}': {Error}",
            run.ItemId.SanitizeLogValue(),
            recipient.Key.SanitizeLogValue(),
            result.Error.SanitizeLogValue());

        return RecipientOutcome.Failed;
    }

    private async Task<OutreachBatchOutcome> FailAsync(OutreachBatch batch, OutreachRun run, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Outreach batch {Sequence} of run '{RunId}' failed.",
            batch.Sequence,
            run.ItemId.SanitizeLogValue());

        var status = await _queue.RetryAsync(batch, exception.Message, _backoff, update: item => CopyProgress(batch, item), cancellationToken: cancellationToken);

        if (status != LeasedWorkItemStatus.DeadLettered)
        {
            return OutreachBatchOutcome.Retrying;
        }

        // The batch kept failing, so the run stops here rather than skipping a page of its audience. The retry
        // committed, so the run is read again before it is saved.
        run = await _runStore.FindByIdAsync(run.ItemId, cancellationToken);

        if (run is null || run.IsFinished)
        {
            return OutreachBatchOutcome.Abandoned;
        }

        run.Status = OutreachRunStatus.Failed;
        run.LastError = exception.Message;
        run.CompletedUtc = _clock.UtcNow;
        run.ModifiedUtc = run.CompletedUtc;

        await _runStore.UpdateAsync(run, cancellationToken);
        await NotifyAsync(run, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return OutreachBatchOutcome.Abandoned;
    }

    private async Task NotifyAsync(OutreachRun run, CancellationToken cancellationToken)
    {
        foreach (var observer in _observers)
        {
            await observer.RunUpdatedAsync(run, cancellationToken);
        }
    }

    private enum RecipientOutcome
    {
        Sent,
        Skipped,
        Failed,
    }
}

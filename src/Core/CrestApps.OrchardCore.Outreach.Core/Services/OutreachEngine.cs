using CrestApps.Core;
using CrestApps.OrchardCore.Outreach.Core.Models;
using CrestApps.OrchardCore.Outreach.Models;
using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Outreach.Core.Services;

/// <summary>
/// The default <see cref="IOutreachEngine"/>: stores a run and queues its first batch; <see cref="OutreachBatchProcessor"/>
/// sends the batches.
/// </summary>
public sealed class OutreachEngine : IOutreachEngine
{
    private readonly IOutreachRunStore _runStore;
    private readonly CatalogLeasedWorkQueue<OutreachBatch> _batchQueue;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutreachEngine"/> class.
    /// </summary>
    /// <param name="runStore">The run store.</param>
    /// <param name="batchStore">The batch store.</param>
    /// <param name="session">The caller's session.</param>
    /// <param name="clock">The clock.</param>
    public OutreachEngine(IOutreachRunStore runStore, IOutreachBatchStore batchStore, ISession session, IClock clock)
    {
        _runStore = runStore;
        _batchQueue = OutreachBatchQueue.Create(batchStore, session, clock);
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<OutreachRun> StartAsync(OutreachRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(run.AudienceSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(run.Sender);

        var now = _clock.UtcNow;

        if (string.IsNullOrEmpty(run.ItemId))
        {
            run.ItemId = UniqueId.GenerateId();
        }

        if (run.PageSize <= 0)
        {
            run.PageSize = OutreachConstants.DefaultPageSize;
        }

        run.Template ??= new OutreachTemplate();
        run.Status = OutreachRunStatus.Queued;
        run.CreatedUtc = now;
        run.ModifiedUtc = now;

        await _runStore.CreateAsync(run, cancellationToken);
        await _batchQueue.EnqueueAsync(new OutreachBatch
        {
            RunId = run.ItemId,
            Sequence = run.BatchesCompleted + 1,
            Cursor = run.Cursor,
            CreatedUtc = now,
        }, cancellationToken);

        return run;
    }

    /// <inheritdoc/>
    public async Task<OutreachRun> FindRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(runId))
        {
            return null;
        }

        return await _runStore.FindByIdAsync(runId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> CancelAsync(string runId, CancellationToken cancellationToken = default)
    {
        var run = await FindRunAsync(runId, cancellationToken);

        if (run is null || run.IsFinished)
        {
            return false;
        }

        // The batches still queued see the canceled run when they are claimed and end without sending.
        run.Status = OutreachRunStatus.Canceled;
        run.CompletedUtc = _clock.UtcNow;
        run.ModifiedUtc = run.CompletedUtc;

        await _runStore.UpdateAsync(run, cancellationToken);

        return true;
    }
}

using CrestApps.OrchardCore.Asterisk.Indexes;
using CrestApps.OrchardCore.Asterisk.Models;
using YesSql;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// YesSql-backed implementation of <see cref="IAsteriskRecordingIngestJobStore"/>. Every mutating operation
/// commits in its OWN isolated session created from the tenant <see cref="IStore"/>, so a job becomes durable
/// immediately, independent of the ambient request scope. Because all sessions are opened from the tenant
/// store, operations are inherently isolated to the current tenant and never observe or mutate another
/// tenant's jobs.
/// </summary>
public sealed class AsteriskRecordingIngestJobStore : IAsteriskRecordingIngestJobStore
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskRecordingIngestJobStore"/> class.
    /// </summary>
    /// <param name="store">The tenant YesSql store used to open isolated, immediately committed sessions.</param>
    public AsteriskRecordingIngestJobStore(IStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task EnqueueAsync(
        string interactionId,
        string recordingName,
        string format,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(recordingName);

        await using var session = _store.CreateSession();

        // Enqueue is idempotent per recording: a stop can be retried, and the deterministic recording name means
        // a second enqueue must not create a duplicate job or reset the progress of an in-flight ingestion.
        var existing = await session
            .Query<AsteriskRecordingIngestJob, AsteriskRecordingIngestJobIndex>(index =>
                index.RecordingName == recordingName)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return;
        }

        await session.SaveAsync(new AsteriskRecordingIngestJob
        {
            InteractionId = interactionId,
            RecordingName = recordingName,
            Format = format,
            Status = RecordingIngestJobStatus.Pending,
            AttemptCount = 0,
            NextAttemptUtc = nowUtc,
            CreatedUtc = nowUtc,
        }, cancellationToken: cancellationToken);

        await session.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AsteriskRecordingIngestJob>> GetDueAsync(
        DateTime nowUtc,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? 100 : maxCount;

        await using var session = _store.CreateSession();
        var jobs = await session
            .Query<AsteriskRecordingIngestJob, AsteriskRecordingIngestJobIndex>(index =>
                index.Status == RecordingIngestJobStatus.Pending &&
                index.NextAttemptUtc <= nowUtc)
            .OrderBy(index => index.NextAttemptUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return jobs.ToList();
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(AsteriskRecordingIngestJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        await using var session = _store.CreateSession();

        // Re-materialize the job inside this session by its stable recording-name key so the update targets a
        // tracked instance and commits durably, even when the caller holds a detached copy from an earlier
        // isolated session.
        var tracked = await session
            .Query<AsteriskRecordingIngestJob, AsteriskRecordingIngestJobIndex>(index =>
                index.RecordingName == job.RecordingName)
            .FirstOrDefaultAsync(cancellationToken);

        if (tracked is null)
        {
            await session.SaveAsync(job, cancellationToken: cancellationToken);
        }
        else
        {
            tracked.InteractionId = job.InteractionId;
            tracked.Format = job.Format;
            tracked.Status = job.Status;
            tracked.AttemptCount = job.AttemptCount;
            tracked.NextAttemptUtc = job.NextAttemptUtc;
            tracked.MediaReference = job.MediaReference;
            tracked.MediaStored = job.MediaStored;
            tracked.LastError = job.LastError;
            tracked.ModifiedUtc = job.ModifiedUtc;
            await session.SaveAsync(tracked, cancellationToken: cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AsteriskRecordingIngestJob> GetByRecordingNameAsync(
        string recordingName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(recordingName))
        {
            return null;
        }

        await using var session = _store.CreateSession();

        return await session
            .Query<AsteriskRecordingIngestJob, AsteriskRecordingIngestJobIndex>(index =>
                index.RecordingName == recordingName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

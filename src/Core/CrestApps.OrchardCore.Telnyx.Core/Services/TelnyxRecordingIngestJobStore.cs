using CrestApps.OrchardCore.Telnyx.Indexes;
using CrestApps.OrchardCore.Telnyx.Models;
using YesSql;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// YesSql-backed implementation of <see cref="ITelnyxRecordingIngestJobStore"/>.
/// <para>
/// Enqueue joins the AMBIENT request session -- the same unit of work the provider's recording-saved handler
/// already uses to stamp the interaction -- so the job is written and committed as part of that one transaction.
/// This is deliberate: the enqueue runs inside a provider webhook whose ambient session, once it flushes any
/// write, holds SQLite's single WAL writer lock until the request commits. A separate (isolated) session opened
/// during that window can never acquire the write lock, and because both connections live in the SAME process
/// SQLite returns "database is locked" IMMEDIATELY -- it will not run the busy-timeout handler when doing so
/// would deadlock -- so a self-contending isolated enqueue fails on every retry. Writing on the ambient session
/// makes the job part of the one transaction instead of contending with it.
/// </para>
/// <para>
/// The background sweep operations (<see cref="GetDueAsync"/>, <see cref="UpdateAsync"/>) instead use their own
/// short-lived isolated sessions from the tenant <see cref="IStore"/>, so a per-job write commits and releases
/// the writer lock immediately rather than being held open across the slow recording download between them.
/// All sessions are tenant-scoped, so operations never observe or mutate another tenant's jobs.
/// </para>
/// </summary>
public sealed class TelnyxRecordingIngestJobStore : ITelnyxRecordingIngestJobStore
{
    private const int UpdateMaxAttempts = 5;

    private readonly IStore _store;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxRecordingIngestJobStore"/> class.
    /// </summary>
    /// <param name="store">The tenant YesSql store used to open short isolated sessions for the background sweep.</param>
    /// <param name="session">The ambient request session that <see cref="EnqueueAsync"/> writes the job into.</param>
    public TelnyxRecordingIngestJobStore(IStore store, ISession session)
    {
        _store = store;
        _session = session;
    }

    /// <inheritdoc/>
    public async Task EnqueueAsync(
        string interactionId,
        string recordingId,
        string format,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(recordingId);

        // Enqueue is idempotent per recording: Telnyx can redeliver the saved webhook, and the unique recording id
        // means a second enqueue must not create a duplicate job or reset the progress of an in-flight ingestion.
        // The query runs on the ambient session, so it also sees a job this same request has just enqueued.
        var existing = await _session
            .Query<TelnyxRecordingIngestJob, TelnyxRecordingIngestJobIndex>(index =>
                index.RecordingId == recordingId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return;
        }

        // Save into the ambient session only. It is committed with the rest of the request's unit of work when the
        // shell scope disposes -- before the after-commit ingest runs -- so the job is durable by the time it is
        // read back. It is intentionally NOT committed here: forcing a commit would defeat the whole point by
        // opening a second writer against the request's own in-flight transaction.
        await _session.SaveAsync(new TelnyxRecordingIngestJob
        {
            InteractionId = interactionId,
            RecordingId = recordingId,
            Format = format,
            Status = TelnyxRecordingIngestJobStatus.Pending,
            AttemptCount = 0,
            NextAttemptUtc = nowUtc,
            CreatedUtc = nowUtc,
        }, cancellationToken: cancellationToken);
    }

    private static bool IsTransientDatabaseLock(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;

            if (!string.IsNullOrEmpty(message) &&
                (message.Contains("database is locked", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("database table is locked", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("database is busy", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TelnyxRecordingIngestJob>> GetDueAsync(
        DateTime nowUtc,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? 100 : maxCount;

        await using var session = _store.CreateSession();
        var jobs = await session
            .Query<TelnyxRecordingIngestJob, TelnyxRecordingIngestJobIndex>(index =>
                index.Status == TelnyxRecordingIngestJobStatus.Pending &&
                index.NextAttemptUtc <= nowUtc)
            .OrderBy(index => index.NextAttemptUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return jobs.ToList();
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(TelnyxRecordingIngestJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        // The sweep's isolated write can still momentarily lose the writer lock to another scope's in-flight
        // transaction; when that other writer is in this same process SQLite returns "database is locked" at once
        // rather than waiting on busy_timeout. A fresh session per attempt makes retrying safe and idempotent.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var session = _store.CreateSession();

                // Re-materialize the job inside this session by its stable recording-id key so the update targets a
                // tracked instance and commits durably, even when the caller holds a detached copy from an earlier
                // isolated session.
                var tracked = await session
                    .Query<TelnyxRecordingIngestJob, TelnyxRecordingIngestJobIndex>(index =>
                        index.RecordingId == job.RecordingId)
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

                return;
            }
            catch (Exception ex) when (attempt < UpdateMaxAttempts && IsTransientDatabaseLock(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken);
            }
        }
    }
}

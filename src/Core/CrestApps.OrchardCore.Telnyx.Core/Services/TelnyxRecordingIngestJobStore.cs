using CrestApps.OrchardCore.Telnyx.Indexes;
using CrestApps.OrchardCore.Telnyx.Models;
using YesSql;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// YesSql-backed implementation of <see cref="ITelnyxRecordingIngestJobStore"/>. Every mutating operation
/// commits in its OWN isolated session created from the tenant <see cref="IStore"/>, so a job becomes durable
/// immediately, independent of the ambient request scope. Because all sessions are opened from the tenant
/// store, operations are inherently isolated to the current tenant and never observe or mutate another tenant's
/// jobs.
/// </summary>
public sealed class TelnyxRecordingIngestJobStore : ITelnyxRecordingIngestJobStore
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxRecordingIngestJobStore"/> class.
    /// </summary>
    /// <param name="store">The tenant YesSql store used to open isolated, immediately committed sessions.</param>
    public TelnyxRecordingIngestJobStore(IStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    private const int EnqueueMaxAttempts = 5;

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
        // This dedup read runs in its OWN session that is fully disposed before the write session opens. That
        // separation is what keeps the enqueue reliable under load: under SQLite WAL a busy_timeout lets a writer
        // WAIT for the single write lock, but only if the connection is not already holding a read snapshot it must
        // promote to a write -- two connections that each hold a read snapshot and then both try to promote
        // deadlock, and SQLite breaks that deadlock by returning "database is locked" IMMEDIATELY, ignoring the
        // busy_timeout. By reading here and writing on a fresh, read-free connection below, the write connection has
        // no snapshot to promote, so it queues on the write lock (honoring busy_timeout) instead of failing at once.
        await using (var readSession = _store.CreateSession())
        {
            var existing = await readSession
                .Query<TelnyxRecordingIngestJob, TelnyxRecordingIngestJobIndex>(index =>
                    index.RecordingId == recordingId)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is not null)
            {
                return;
            }
        }

        // Losing this enqueue means the recording is never ingested and the voicemail can never be played, so a
        // transient database lock (SQLite serialises writers) must not drop it. Each attempt uses a fresh write-only
        // session and the operation is idempotent per recording, so retrying is safe.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var session = _store.CreateSession();

                await session.SaveAsync(new TelnyxRecordingIngestJob
                {
                    InteractionId = interactionId,
                    RecordingId = recordingId,
                    Format = format,
                    Status = TelnyxRecordingIngestJobStatus.Pending,
                    AttemptCount = 0,
                    NextAttemptUtc = nowUtc,
                    CreatedUtc = nowUtc,
                }, cancellationToken: cancellationToken);

                await session.SaveChangesAsync(cancellationToken);

                return;
            }
            catch (Exception ex) when (attempt < EnqueueMaxAttempts && IsTransientDatabaseLock(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken);
            }
        }
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
    }
}

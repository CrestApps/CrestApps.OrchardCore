using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;

namespace CrestApps.OrchardCore.Tests.Doubles;

internal sealed class FakeAsteriskRecordingIngestJobStore : IAsteriskRecordingIngestJobStore
{
    private readonly Dictionary<string, AsteriskRecordingIngestJob> _jobs = new(StringComparer.Ordinal);

    public IReadOnlyList<AsteriskRecordingIngestJob> Jobs
        => [.. _jobs.Values.Select(Clone)];

    public Task EnqueueAsync(
        string interactionId,
        string recordingName,
        string format,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (!_jobs.ContainsKey(recordingName))
        {
            _jobs[recordingName] = new AsteriskRecordingIngestJob
            {
                InteractionId = interactionId,
                RecordingName = recordingName,
                Format = format,
                Status = RecordingIngestJobStatus.Pending,
                AttemptCount = 0,
                NextAttemptUtc = nowUtc,
                CreatedUtc = nowUtc,
            };
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AsteriskRecordingIngestJob>> GetDueAsync(
        DateTime nowUtc,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var matches = _jobs.Values
            .Where(job => job.Status == RecordingIngestJobStatus.Pending && job.NextAttemptUtc <= nowUtc)
            .OrderBy(job => job.NextAttemptUtc)
            .Take(maxCount)
            .Select(Clone)
            .ToList();

        return Task.FromResult<IReadOnlyList<AsteriskRecordingIngestJob>>(matches);
    }

    public Task UpdateAsync(AsteriskRecordingIngestJob job, CancellationToken cancellationToken = default)
    {
        _jobs[job.RecordingName] = Clone(job);

        return Task.CompletedTask;
    }

    public Task<AsteriskRecordingIngestJob> GetByRecordingNameAsync(
        string recordingName,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_jobs.TryGetValue(recordingName, out var job) ? Clone(job) : null);
    }

    private static AsteriskRecordingIngestJob Clone(AsteriskRecordingIngestJob job)
    {
        return new AsteriskRecordingIngestJob
        {
            InteractionId = job.InteractionId,
            RecordingName = job.RecordingName,
            Format = job.Format,
            Status = job.Status,
            AttemptCount = job.AttemptCount,
            NextAttemptUtc = job.NextAttemptUtc,
            MediaReference = job.MediaReference,
            MediaStored = job.MediaStored,
            LastError = job.LastError,
            CreatedUtc = job.CreatedUtc,
            ModifiedUtc = job.ModifiedUtc,
        };
    }
}

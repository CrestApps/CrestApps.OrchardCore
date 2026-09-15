using CrestApps.OrchardCore.Telnyx.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Telnyx.Indexes;

/// <summary>
/// Maps <see cref="TelnyxRecordingIngestJob"/> documents to the <see cref="TelnyxRecordingIngestJobIndex"/>.
/// </summary>
public sealed class TelnyxRecordingIngestJobIndexProvider : IndexProvider<TelnyxRecordingIngestJob>
{
    /// <inheritdoc/>
    public override void Describe(DescribeContext<TelnyxRecordingIngestJob> context)
    {
        context
            .For<TelnyxRecordingIngestJobIndex>()
            .Map(job => new TelnyxRecordingIngestJobIndex
            {
                RecordingId = job.RecordingId,
                Status = job.Status,
                NextAttemptUtc = job.NextAttemptUtc,
            });
    }
}

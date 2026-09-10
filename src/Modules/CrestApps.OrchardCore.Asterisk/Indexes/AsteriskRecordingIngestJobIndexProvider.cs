using CrestApps.OrchardCore.Asterisk.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Asterisk.Indexes;

/// <summary>
/// Maps <see cref="AsteriskRecordingIngestJob"/> documents to the <see cref="AsteriskRecordingIngestJobIndex"/>.
/// </summary>
public sealed class AsteriskRecordingIngestJobIndexProvider : IndexProvider<AsteriskRecordingIngestJob>
{
    /// <inheritdoc/>
    public override void Describe(DescribeContext<AsteriskRecordingIngestJob> context)
    {
        context
            .For<AsteriskRecordingIngestJobIndex>()
            .Map(job => new AsteriskRecordingIngestJobIndex
            {
                RecordingName = job.RecordingName,
                Status = job.Status,
                NextAttemptUtc = job.NextAttemptUtc,
            });
    }
}

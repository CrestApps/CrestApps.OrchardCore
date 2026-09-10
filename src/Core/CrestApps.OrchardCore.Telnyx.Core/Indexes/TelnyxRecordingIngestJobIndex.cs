using CrestApps.OrchardCore.Telnyx.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Telnyx.Indexes;

/// <summary>
/// YesSql index used to query durable Telnyx recording ingest jobs by recording id, dispatch status, and next
/// attempt time.
/// </summary>
public sealed class TelnyxRecordingIngestJobIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the Telnyx recording id that uniquely identifies the job.
    /// </summary>
    public string RecordingId { get; set; }

    /// <summary>
    /// Gets or sets the dispatch state of the job.
    /// </summary>
    public TelnyxRecordingIngestJobStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the next ingest attempt is due.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }
}

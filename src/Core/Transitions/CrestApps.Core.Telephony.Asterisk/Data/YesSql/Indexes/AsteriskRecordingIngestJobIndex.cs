using CrestApps.Core.Telephony.Asterisk.Models;
using YesSql.Indexes;

namespace CrestApps.Core.Telephony.Asterisk.Data.YesSql.Indexes;

/// <summary>
/// YesSql index used to query durable recording ingest jobs by recording name, dispatch status, and next
/// attempt time.
/// </summary>
public sealed class AsteriskRecordingIngestJobIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the deterministic Asterisk recording name that uniquely identifies the job.
    /// </summary>
    public string RecordingName { get; set; }

    /// <summary>
    /// Gets or sets the dispatch state of the job.
    /// </summary>
    public RecordingIngestJobStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the next ingest attempt is due.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }
}

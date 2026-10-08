using System.Text.Json.Serialization;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Serialization;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The pacing record of one over-dialing campaign queue: how many cycles have run and what the last one decided.
/// </summary>
/// <remarks>
/// Every over-dial cycle rewrites this record under a concurrency check before any of its calls is dispatched. Two
/// nodes that pace the same queue at once therefore cannot both commit: the loser's transaction is rolled back with the
/// dials it staged, and calls are only placed after a commit, so the queue never runs past the calculated number of
/// calls in flight because of a race. The pacing lock is only an accelerator on top of this.
/// </remarks>
public sealed class PredictivePacingState : CatalogItem
{
    /// <summary>
    /// Gets or sets the campaign queue the record paces.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the dialer profile the last cycle was paced with.
    /// </summary>
    public string DialerProfileId { get; set; }

    /// <summary>
    /// Gets or sets the number of cycles committed for the queue. It changes on every cycle, so every cycle writes the
    /// record and is therefore checked against a concurrent one.
    /// </summary>
    public long Sequence { get; set; }

    /// <summary>
    /// Gets or sets when the last cycle ran.
    /// </summary>
    [JsonConverter(typeof(PreciseUtcDateTimeJsonConverter))]
    public DateTime? LastCycleUtc { get; set; }

    /// <summary>
    /// Gets or sets what the last cycle measured and decided.
    /// </summary>
    public PredictivePacingSnapshot LastDecision { get; set; }
}

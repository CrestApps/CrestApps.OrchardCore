using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

internal sealed class AgentPresenceInterval
{
    public string AgentId { get; set; }

    public AgentPresenceStatus Status { get; set; }

    public string Reason { get; set; }

    public IList<string> QueueIds { get; set; } = [];

    public IList<string> CampaignIds { get; set; } = [];

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the interval was built from the state audit rather than the older presence events,
    /// which do not record reservations, calls or wrap-up.
    /// </summary>
    public bool FromAudit { get; set; }

    public double DurationSeconds => Math.Max(0d, (EndUtc - StartUtc).TotalSeconds);
}

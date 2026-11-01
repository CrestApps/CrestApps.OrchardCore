using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Models;

/// <summary>
/// Records that an out-of-adherence episode was alerted, so each episode raises its alert once.
/// </summary>
public sealed class AdherenceAlert : CatalogItem
{
    /// <summary>
    /// Gets or sets the episode's key: the worker, the scheduled activity's start and when they went out.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets when the alert was raised.
    /// </summary>
    public DateTime RaisedUtc { get; set; }
}

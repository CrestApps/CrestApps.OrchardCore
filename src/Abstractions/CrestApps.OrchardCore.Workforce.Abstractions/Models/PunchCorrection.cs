using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A worker's request to add a punch they forgot.
/// </summary>
public sealed class PunchCorrection : CatalogItem
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the worker's team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the punch to add.
    /// </summary>
    public TimePunchKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the punch to add.
    /// </summary>
    public DateTime Utc { get; set; }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets where it stands.
    /// </summary>
    public PunchCorrectionStatus Status { get; set; } = PunchCorrectionStatus.Pending;

    /// <summary>
    /// Gets or sets the punch it added.
    /// </summary>
    public string PunchId { get; set; }

    /// <summary>
    /// Gets or sets who decided it.
    /// </summary>
    public string DecidedBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was decided.
    /// </summary>
    public DateTime? DecidedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was asked for.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

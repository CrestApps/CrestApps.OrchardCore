namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A worker's claim on an open shift.
/// </summary>
public sealed class OpenShiftClaim
{
    /// <summary>
    /// Gets or sets the worker who claimed it.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets where the claim stands.
    /// </summary>
    public OpenShiftClaimStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when it was claimed.
    /// </summary>
    public DateTime ClaimedUtc { get; set; }

    /// <summary>
    /// Gets or sets who decided.
    /// </summary>
    public string DecidedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was decided.
    /// </summary>
    public DateTime? DecidedUtc { get; set; }

    /// <summary>
    /// Gets or sets a note from whoever decided.
    /// </summary>
    public string Note { get; set; }
}

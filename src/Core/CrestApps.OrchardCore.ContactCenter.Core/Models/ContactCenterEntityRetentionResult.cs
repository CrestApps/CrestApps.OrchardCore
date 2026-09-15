namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Reports what one retention policy did during a retention cycle.
/// </summary>
public sealed class ContactCenterEntityRetentionResult
{
    /// <summary>
    /// Gets or sets the technical name of the entity the policy purges.
    /// </summary>
    public string EntityName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether purging is enabled for this entity. When it is disabled the
    /// entity's records are kept indefinitely and no cutoff was computed.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the cutoff that was applied, or <see langword="null"/> when purging is disabled.
    /// </summary>
    public DateTime? CutoffUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of records purged.
    /// </summary>
    public int PurgedCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the drain stopped before the entity was empty because the cycle
    /// budget ran out or the cycle was canceled. It is the signal that the configured budget is too small for
    /// the volume, which would otherwise be invisible.
    /// </summary>
    public bool WorkRemains { get; set; }
}

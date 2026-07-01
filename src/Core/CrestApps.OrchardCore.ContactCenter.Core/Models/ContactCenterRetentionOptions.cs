namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Configures Contact Center data-governance retention windows. A value of zero disables purging so
/// data is kept indefinitely.
/// </summary>
public sealed class ContactCenterRetentionOptions
{
    /// <summary>
    /// Gets or sets the number of days to retain durable interaction events before they are purged.
    /// </summary>
    public int InteractionEventRetentionDays { get; set; }
}

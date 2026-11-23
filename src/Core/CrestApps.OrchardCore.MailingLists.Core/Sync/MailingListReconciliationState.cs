namespace CrestApps.OrchardCore.MailingLists.Core.Sync;

/// <summary>
/// Where the daily reconciliation of a list with its provider stands, kept in the list's properties.
/// </summary>
public sealed class MailingListReconciliationState
{
    /// <summary>
    /// Gets or sets when the last reconciliation started (UTC); the next one reads the provider's changes since then.
    /// </summary>
    public DateTime? LastRunUtc { get; set; }

    /// <summary>
    /// Gets or sets how many changes the last reconciliation applied.
    /// </summary>
    public int LastApplied { get; set; }

    /// <summary>
    /// Gets or sets the error of the last reconciliation, when it failed.
    /// </summary>
    public string LastError { get; set; }
}

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// The declared lengths of the dialer decision log's text columns, shared by the migration that creates them and the
/// index provider that cuts each value to fit.
/// </summary>
internal static class DialerDecisionIndexColumns
{
    /// <summary>
    /// The length of the event type column.
    /// </summary>
    public const int EventTypeLength = 128;

    /// <summary>
    /// The length of the queue column, which holds a campaign's virtual queue for campaign work.
    /// </summary>
    public const int QueueIdLength = ContactCenterStorage.QueueIdLength;

    /// <summary>
    /// The length of the record, queue item, campaign and agent columns. These are the platform's own 26-character
    /// identifiers; the room above that keeps a longer identifier written by an integration from being refused.
    /// </summary>
    public const int IdLength = 64;

    /// <summary>
    /// The length of the reason column.
    /// </summary>
    public const int ReasonLength = 256;
}

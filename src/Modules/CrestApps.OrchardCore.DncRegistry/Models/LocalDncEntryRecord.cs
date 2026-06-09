namespace CrestApps.OrchardCore.DncRegistry.Models;

/// <summary>
/// Represents a single indexed phone number record stored inside a local DNC entry batch.
/// </summary>
public sealed class LocalDncEntryRecord
{
    /// <summary>
    /// Gets or sets the unique identifier for this phone number record.
    /// </summary>
    public string EntryId { get; set; }

    /// <summary>
    /// Gets or sets the normalized phone number in E.164 format.
    /// </summary>
    public string PhoneNumber { get; set; }
}

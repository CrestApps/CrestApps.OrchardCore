namespace CrestApps.OrchardCore.ContactCenter.Maintenance;

/// <summary>
/// Reports the persisted document count of a single Contact Center data set at a point in time.
/// </summary>
public sealed class ContactCenterPreviewDataSetCount
{
    /// <summary>
    /// Gets the data set key, which is the persisted document type name.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the governance category key that classifies this data set.
    /// </summary>
    public required string GovernanceCategoryKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether this data set holds operator-authored configuration.
    /// </summary>
    public required bool IsConfiguration { get; init; }

    /// <summary>
    /// Gets the number of persisted documents in this data set.
    /// </summary>
    public required int Count { get; init; }
}

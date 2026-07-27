namespace CrestApps.OrchardCore.ContactCenter.Maintenance;

/// <summary>
/// Reports the outcome of a Contact Center preview export.
/// </summary>
public sealed class ContactCenterPreviewExportReport
{
    /// <summary>
    /// Gets the tenant the export was taken from.
    /// </summary>
    public required string TenantName { get; init; }

    /// <summary>
    /// Gets the UTC instant the export was taken.
    /// </summary>
    public required DateTime TakenUtc { get; init; }

    /// <summary>
    /// Gets the per-data-set document counts captured by the export.
    /// </summary>
    public required IReadOnlyList<ContactCenterPreviewDataSetCount> DataSets { get; init; }

    /// <summary>
    /// Gets the total number of documents written to the export.
    /// </summary>
    public required int DocumentCount { get; init; }

    /// <summary>
    /// Gets the receipt that binds this export to the exact tenant state it captured. A reset is refused unless
    /// it presents a receipt that still matches the live state, which proves the export is not stale and that
    /// nothing has been written since it was taken.
    /// </summary>
    public required string Receipt { get; init; }
}

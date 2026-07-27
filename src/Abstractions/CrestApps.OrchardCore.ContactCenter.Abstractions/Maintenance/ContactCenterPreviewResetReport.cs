namespace CrestApps.OrchardCore.ContactCenter.Maintenance;

/// <summary>
/// Reports the outcome of a Contact Center preview reset.
/// </summary>
public sealed class ContactCenterPreviewResetReport
{
    /// <summary>
    /// Gets the tenant the reset was requested for.
    /// </summary>
    public required string TenantName { get; init; }

    /// <summary>
    /// Gets the reason the reset was refused, or <see cref="ContactCenterPreviewResetRefusalReason.None"/> when
    /// the reset ran.
    /// </summary>
    public required ContactCenterPreviewResetRefusalReason RefusalReason { get; init; }

    /// <summary>
    /// Gets the scope that was requested.
    /// </summary>
    public required ContactCenterPreviewResetScope Scope { get; init; }

    /// <summary>
    /// Gets the number of documents deleted from each data set, keyed by data set key. It is empty when the
    /// reset was refused.
    /// </summary>
    public required IReadOnlyDictionary<string, int> DeletedByDataSet { get; init; }

    /// <summary>
    /// Gets the data set keys that were preserved because the requested scope excluded them.
    /// </summary>
    public required IReadOnlyList<string> PreservedDataSetKeys { get; init; }

    /// <summary>
    /// Gets a value indicating whether the reset ran.
    /// </summary>
    public bool Succeeded => RefusalReason == ContactCenterPreviewResetRefusalReason.None;

    /// <summary>
    /// Gets the total number of documents deleted.
    /// </summary>
    public int DeletedCount => DeletedByDataSet.Values.Sum();
}

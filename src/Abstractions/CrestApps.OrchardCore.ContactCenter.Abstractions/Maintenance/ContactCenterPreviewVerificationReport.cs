namespace CrestApps.OrchardCore.ContactCenter.Maintenance;

/// <summary>
/// Reports whether a Contact Center preview reset actually emptied everything it claimed to delete.
/// </summary>
public sealed class ContactCenterPreviewVerificationReport
{
    /// <summary>
    /// Gets the tenant that was verified.
    /// </summary>
    public required string TenantName { get; init; }

    /// <summary>
    /// Gets the scope the verification was performed against.
    /// </summary>
    public required ContactCenterPreviewResetScope Scope { get; init; }

    /// <summary>
    /// Gets the live per-data-set document counts observed during verification.
    /// </summary>
    public required IReadOnlyList<ContactCenterPreviewDataSetCount> DataSets { get; init; }

    /// <summary>
    /// Gets the keys of the data sets that were expected to be empty but still hold documents.
    /// </summary>
    public required IReadOnlyList<string> ResidualDataSetKeys { get; init; }

    /// <summary>
    /// Gets a value indicating whether every data set in scope is empty.
    /// </summary>
    public bool IsClean => ResidualDataSetKeys.Count == 0;
}

namespace CrestApps.OrchardCore.ContactCenter.Maintenance;

/// <summary>
/// Describes the live state of the Contact Center preview maintenance tooling for the current tenant.
/// </summary>
public sealed class ContactCenterPreviewMaintenanceStatus
{
    /// <summary>
    /// Gets the current tenant name, which is also the confirmation token a reset must present.
    /// </summary>
    public required string TenantName { get; init; }

    /// <summary>
    /// Gets the live per-data-set document counts.
    /// </summary>
    public required IReadOnlyList<ContactCenterPreviewDataSetCount> DataSets { get; init; }

    /// <summary>
    /// Gets the Contact Center feature identifiers that participate in quiesce.
    /// </summary>
    public required IReadOnlyList<string> ParticipatingFeatureIds { get; init; }

    /// <summary>
    /// Gets the feature identifiers whose work admission is currently closed.
    /// </summary>
    public required IReadOnlyList<string> QuiescedFeatureIds { get; init; }

    /// <summary>
    /// Gets a value indicating whether reset is enabled for this tenant.
    /// </summary>
    public required bool IsResetAllowed { get; init; }

    /// <summary>
    /// Gets a value indicating whether reset would be refused because the host runs in the Production
    /// environment.
    /// </summary>
    public required bool IsProductionRefusal { get; init; }

    /// <summary>
    /// Gets a value indicating whether every participating feature is quiesced.
    /// </summary>
    public bool IsQuiesced => ParticipatingFeatureIds.Count > 0 && QuiescedFeatureIds.Count == ParticipatingFeatureIds.Count;
}

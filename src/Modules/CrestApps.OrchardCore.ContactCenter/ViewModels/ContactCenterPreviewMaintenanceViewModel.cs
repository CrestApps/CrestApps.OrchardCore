using CrestApps.OrchardCore.ContactCenter.Maintenance;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Presents the Contact Center preview maintenance state to an operator.
/// </summary>
public class ContactCenterPreviewMaintenanceViewModel
{
    /// <summary>
    /// Gets or sets the tenant name that the operator must type to confirm a reset.
    /// </summary>
    public string TenantName { get; set; }

    /// <summary>
    /// Gets or sets the live per-data-set document counts.
    /// </summary>
    public IReadOnlyList<ContactCenterPreviewDataSetCount> DataSets { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether reset is enabled for this tenant.
    /// </summary>
    public bool IsResetAllowed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether reset is refused because the host runs in Production.
    /// </summary>
    public bool IsProductionRefusal { get; set; }

    /// <summary>
    /// Gets or sets the Contact Center feature identifiers that participate in quiesce.
    /// </summary>
    public IReadOnlyList<string> ParticipatingFeatureIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the feature identifiers whose work admission is currently closed.
    /// </summary>
    public IReadOnlyList<string> QuiescedFeatureIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the receipt of the most recent export taken in this browser session.
    /// </summary>
    public string ExportReceipt { get; set; }

    /// <summary>
    /// Gets or sets the confirmation token typed by the operator.
    /// </summary>
    public string ConfirmationToken { get; set; }

    /// <summary>
    /// Gets or sets the requested reset scope.
    /// </summary>
    public ContactCenterPreviewResetScope Scope { get; set; }

    /// <summary>
    /// Gets a value indicating whether every participating feature is quiesced.
    /// </summary>
    public bool IsQuiesced => ParticipatingFeatureIds.Count > 0 && QuiescedFeatureIds.Count == ParticipatingFeatureIds.Count;
}

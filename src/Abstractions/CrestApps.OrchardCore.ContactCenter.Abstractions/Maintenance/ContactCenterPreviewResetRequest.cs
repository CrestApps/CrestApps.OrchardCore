namespace CrestApps.OrchardCore.ContactCenter.Maintenance;

/// <summary>
/// Describes an operator's request to reset the Contact Center data of a preview tenant.
/// </summary>
public sealed class ContactCenterPreviewResetRequest
{
    /// <summary>
    /// Gets the confirmation token typed by the operator. It must equal the tenant name.
    /// </summary>
    public string ConfirmationToken { get; init; }

    /// <summary>
    /// Gets the receipt returned by the export that must precede the reset.
    /// </summary>
    public string ExportReceipt { get; init; }

    /// <summary>
    /// Gets the scope of the reset.
    /// </summary>
    public ContactCenterPreviewResetScope Scope { get; init; }
}

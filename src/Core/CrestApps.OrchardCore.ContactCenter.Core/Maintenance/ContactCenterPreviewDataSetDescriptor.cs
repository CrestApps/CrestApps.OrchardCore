namespace CrestApps.OrchardCore.ContactCenter.Core.Maintenance;

/// <summary>
/// Declares one persisted Contact Center document type that the preview maintenance tooling exports and resets.
/// </summary>
public sealed class ContactCenterPreviewDataSetDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterPreviewDataSetDescriptor"/> class.
    /// </summary>
    /// <param name="documentType">The persisted document type.</param>
    /// <param name="governanceCategoryKey">The governance catalog category key that classifies the document type.</param>
    /// <param name="isConfiguration">Whether the document type holds operator-authored configuration.</param>
    public ContactCenterPreviewDataSetDescriptor(
        Type documentType,
        string governanceCategoryKey,
        bool isConfiguration)
    {
        ArgumentNullException.ThrowIfNull(documentType);
        ArgumentException.ThrowIfNullOrEmpty(governanceCategoryKey);

        DocumentType = documentType;
        GovernanceCategoryKey = governanceCategoryKey;
        IsConfiguration = isConfiguration;
    }

    /// <summary>
    /// Gets the persisted document type.
    /// </summary>
    public Type DocumentType { get; }

    /// <summary>
    /// Gets the governance catalog category key that classifies this document type.
    /// </summary>
    public string GovernanceCategoryKey { get; }

    /// <summary>
    /// Gets a value indicating whether this document type holds operator-authored configuration and is
    /// therefore preserved by an operational-scope reset.
    /// </summary>
    public bool IsConfiguration { get; }
}

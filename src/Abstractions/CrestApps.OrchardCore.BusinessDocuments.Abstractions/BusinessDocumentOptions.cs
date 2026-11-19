namespace CrestApps.OrchardCore.BusinessDocuments;

/// <summary>
/// The kinds of business documents the enabled features print.
/// </summary>
public sealed class BusinessDocumentOptions
{
    /// <summary>
    /// Gets the registered kinds, by name.
    /// </summary>
    public Dictionary<string, BusinessDocumentKind> Kinds { get; } = new(StringComparer.OrdinalIgnoreCase);
}

using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.BusinessDocuments.Core.Indexes;

/// <summary>
/// Finds document templates by name, and the default ones.
/// </summary>
public sealed class BusinessDocumentTemplateIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the name of the template.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the template is the default for its kinds.
    /// </summary>
    public bool IsDefault { get; set; }
}

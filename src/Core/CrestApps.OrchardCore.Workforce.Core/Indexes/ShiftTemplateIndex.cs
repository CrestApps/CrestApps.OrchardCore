using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of shift templates.
/// </summary>
public sealed class ShiftTemplateIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the template name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets whether the template can be used.
    /// </summary>
    public bool Enabled { get; set; }
}

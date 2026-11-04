using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Skills.Core.Indexes;

/// <summary>
/// The YesSql index of certifications.
/// </summary>
public sealed class CertificationIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the certification name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name in upper case, for finding a name whatever its case.
    /// </summary>
    public string NormalizedName { get; set; }

    /// <summary>
    /// Gets or sets whether the certification is enabled.
    /// </summary>
    public bool Enabled { get; set; }
}

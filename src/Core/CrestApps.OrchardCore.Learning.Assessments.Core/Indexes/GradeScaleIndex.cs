using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;

/// <summary>
/// Finds grade scales by name.
/// </summary>
public sealed class GradeScaleIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }
}

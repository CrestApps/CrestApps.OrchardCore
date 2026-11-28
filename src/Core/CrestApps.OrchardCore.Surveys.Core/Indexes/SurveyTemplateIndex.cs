using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Finds survey templates by name and position.
/// </summary>
public sealed class SurveyTemplateIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the unique technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title, cut to 255 characters.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the kind of survey the template makes.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the position in the list.
    /// </summary>
    public int Position { get; set; }
}

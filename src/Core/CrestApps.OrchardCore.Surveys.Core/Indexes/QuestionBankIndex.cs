using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Finds question banks by name and owner.
/// </summary>
public sealed class QuestionBankIndex : CatalogItemIndex
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
    /// Gets or sets the owner as Kind:Id, or null.
    /// </summary>
    public string OwnerRef { get; set; }
}

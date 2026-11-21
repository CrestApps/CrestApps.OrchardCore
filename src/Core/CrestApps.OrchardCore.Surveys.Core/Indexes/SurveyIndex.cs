using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Finds surveys by name, kind, status, owner and change time.
/// </summary>
public sealed class SurveyIndex : CatalogItemIndex
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
    /// Gets or sets the kind.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the owner as Kind:Id, or null.
    /// </summary>
    public string OwnerRef { get; set; }

    /// <summary>
    /// Gets or sets the published version number.
    /// </summary>
    public int PublishedVersionNumber { get; set; }

    /// <summary>
    /// Gets or sets when the survey last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// A visual filtered by the values picked in another visual.
/// </summary>
public sealed class ReportActionTarget
{
    /// <summary>
    /// Gets or sets the identifier of the visual filtered.
    /// </summary>
    public string VisualId { get; set; }

    /// <summary>
    /// Gets or sets the key of the field filtered in the query of that visual (<c>alias.Field</c> or a calculated field
    /// name), or <see langword="null"/> to filter the field the picked column shows.
    /// </summary>
    public string Field { get; set; }
}

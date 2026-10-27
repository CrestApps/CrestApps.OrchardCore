namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Makes a section clickable: the values people pick in it, by clicking a row or a chart mark, filter other sections.
/// </summary>
public sealed class ReportSectionSelection
{
    /// <summary>
    /// Gets or sets the identifiers of the sections the picked values filter.
    /// </summary>
    public IList<string> TargetIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the keys of the values picked now, highlighted in the section.
    /// </summary>
    public IList<string> SelectedKeys { get; set; } = [];
}

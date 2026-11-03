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

    /// <summary>
    /// Gets or sets the values picked now as the section shows them, in the order of <see cref="SelectedKeys"/>, so an
    /// export can say what filtered the sections it targets.
    /// </summary>
    public IList<string> SelectedLabels { get; set; } = [];

    /// <summary>
    /// Gets the values picked now as the section shows them, or their keys when the labels are not all known.
    /// </summary>
    /// <returns>The values picked.</returns>
    public IReadOnlyList<string> GetPickedValues()
    {
        return SelectedLabels.Count == SelectedKeys.Count ? [.. SelectedLabels] : [.. SelectedKeys];
    }
}

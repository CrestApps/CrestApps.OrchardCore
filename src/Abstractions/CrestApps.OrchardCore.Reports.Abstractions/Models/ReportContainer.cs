namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// A framed block of a report page that holds sections under one title, scrolling them when they are taller than it.
/// Exports ignore containers and list every section.
/// </summary>
public sealed class ReportContainer
{
    /// <summary>
    /// Gets or sets the identifier the sections it holds refer to with <see cref="ReportSection.ContainerId"/>.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the title shown in the header band.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the width on a twelve-column grid.
    /// </summary>
    public int Width { get; set; } = 12;

    /// <summary>
    /// Gets or sets the height of the body in pixels, or <c>0</c> to grow with the sections.
    /// </summary>
    public int Height { get; set; }
}

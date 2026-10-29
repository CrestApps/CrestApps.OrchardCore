namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// A framed block of a designed report that holds visuals under one title. A container with a height scrolls its
/// visuals within it, so a report can fit several tables and charts on one screen.
/// </summary>
public sealed class ReportContainerDefinition
{
    /// <summary>
    /// The most pixels a container may be tall.
    /// </summary>
    public const int MaxHeight = 2000;

    /// <summary>
    /// Gets or sets the identifier of the container, unique within the report. Visuals are placed in it by
    /// <see cref="ReportVisualDefinition.ContainerId"/>.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the title shown in the container's header band.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the width of the container on a twelve-column grid. The widths of its visuals divide the
    /// container's own width.
    /// </summary>
    public int Width { get; set; } = 12;

    /// <summary>
    /// Gets or sets the height of the container's body in pixels, which scrolls when its visuals are taller, or
    /// <c>0</c> to grow with its visuals.
    /// </summary>
    public int Height { get; set; }
}

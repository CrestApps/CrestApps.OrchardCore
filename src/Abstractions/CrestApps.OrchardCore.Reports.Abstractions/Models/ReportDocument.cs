namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Represents the uniform result of running a report: an ordered set of sections (metrics, tables, and
/// bars) that the shared renderer displays and the exporter serializes.
/// </summary>
public sealed class ReportDocument
{
    /// <summary>
    /// Gets or sets the sections that make up the report, in display order.
    /// </summary>
    public IList<ReportSection> Sections { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether the report has any content to display.
    /// </summary>
    public bool HasContent
    {
        get
        {
            return Sections.Count > 0;
        }
    }

    /// <summary>
    /// Adds a section to the report and returns the same document for chaining.
    /// </summary>
    /// <param name="section">The section to add. Ignored when <see langword="null"/>.</param>
    /// <returns>The current document.</returns>
    public ReportDocument Add(ReportSection section)
    {
        if (section is not null)
        {
            Sections.Add(section);
        }

        return this;
    }
}

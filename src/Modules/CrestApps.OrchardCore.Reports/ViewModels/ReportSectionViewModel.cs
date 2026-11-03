using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Reports.ViewModels;

/// <summary>
/// One section of a report document as the report page renders it.
/// </summary>
public sealed class ReportSectionViewModel
{
    /// <summary>
    /// Gets or sets the section.
    /// </summary>
    public ReportSection Section { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the section's title is shown above it. The first section of a page
    /// leaves its title to the page heading.
    /// </summary>
    public bool ShowTitle { get; set; }
}

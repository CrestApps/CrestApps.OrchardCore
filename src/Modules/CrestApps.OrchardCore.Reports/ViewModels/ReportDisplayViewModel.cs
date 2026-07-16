using CrestApps.OrchardCore.Reports.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Reports.ViewModels;

/// <summary>
/// The view model for a rendered report page: the report, its filter editor, the resulting document,
/// and the resolved reporting period.
/// </summary>
public sealed class ReportDisplayViewModel
{
    /// <summary>
    /// Gets or sets the report being displayed.
    /// </summary>
    public IReport Report { get; set; }

    /// <summary>
    /// Gets or sets the available export formats for the report.
    /// </summary>
    public IReadOnlyList<IReportExportFormat> ExportFormats { get; set; } = [];

    /// <summary>
    /// Gets or sets the rendered filter editor shape.
    /// </summary>
    public IShape FilterShape { get; set; }

    /// <summary>
    /// Gets or sets the report document to render.
    /// </summary>
    public ReportDocument Document { get; set; }

    /// <summary>
    /// Gets or sets the resolved inclusive lower UTC bound of the reporting period.
    /// </summary>
    public DateTime FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the resolved inclusive upper UTC bound of the reporting period.
    /// </summary>
    public DateTime ToUtc { get; set; }
}

using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;

namespace CrestApps.OrchardCore.Reports.Designer.ViewModels;

/// <summary>
/// The view model of the designer page.
/// </summary>
public class ReportDesignerViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether a view, rather than a report, is being designed.
    /// </summary>
    public bool IsView { get; set; }

    /// <summary>
    /// Gets or sets the report or view being designed.
    /// </summary>
    public ReportDesignerPayload Payload { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the designer may share reports publicly.
    /// </summary>
    public bool CanSharePublicly { get; set; }

    /// <summary>
    /// Gets or sets the role names a report can be shared with.
    /// </summary>
    public IList<string> Roles { get; set; } = [];

    /// <summary>
    /// Gets or sets the categories already used by designed reports, suggested in the category box.
    /// </summary>
    public IList<string> Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets the localized texts and option labels of the designer script.
    /// </summary>
    public IDictionary<string, object> Labels { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// The view model of the designer preview.
/// </summary>
public class ReportDesignerPreviewViewModel
{
    /// <summary>
    /// Gets or sets the run outcome.
    /// </summary>
    public ReportRunResult Run { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a view is being previewed.
    /// </summary>
    public bool IsView { get; set; }
}

/// <summary>
/// The view model of the designed report list.
/// </summary>
public class ReportDesignsIndexViewModel
{
    /// <summary>
    /// Gets or sets the reports the user can see, with what the user may do with each.
    /// </summary>
    public IList<ReportDesignListEntry> Entries { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the user can design reports.
    /// </summary>
    public bool CanDesign { get; set; }

    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Search { get; set; }
}

/// <summary>
/// One entry of the designed report list.
/// </summary>
public class ReportDesignListEntry
{
    /// <summary>
    /// Gets or sets the report.
    /// </summary>
    public ReportDesign Design { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may change the report.
    /// </summary>
    public bool CanEdit { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user owns the report.
    /// </summary>
    public bool IsOwner { get; set; }
}

/// <summary>
/// The view model of the view list.
/// </summary>
public class ReportViewsIndexViewModel
{
    /// <summary>
    /// Gets or sets the views, with whether the user may change each.
    /// </summary>
    public IList<(ReportView View, bool CanEdit)> Entries { get; set; } = [];
}

/// <summary>
/// The view model of a running designed report.
/// </summary>
public class DesignedReportViewModel
{
    /// <summary>
    /// Gets or sets the report.
    /// </summary>
    public ReportDesign Design { get; set; }

    /// <summary>
    /// Gets or sets the run outcome.
    /// </summary>
    public ReportRunResult Run { get; set; }

    /// <summary>
    /// Gets or sets the export formats offered, empty when the viewer may not export.
    /// </summary>
    public IReadOnlyList<IReportExportFormat> ExportFormats { get; set; } = [];

    /// <summary>
    /// Gets or sets the function that builds the export URL of a format.
    /// </summary>
    public Func<string, string> ExportUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL the filter form submits to.
    /// </summary>
    public string FormAction { get; set; }

    /// <summary>
    /// Gets or sets the URL of the designer, when the viewer may change the report.
    /// </summary>
    public string EditUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the page is shown outside the admin.
    /// </summary>
    public bool IsPublic { get; set; }
}

/// <summary>
/// The view model of the exposed filter controls.
/// </summary>
public class ReportFiltersViewModel
{
    /// <summary>
    /// Gets or sets the exposed filters.
    /// </summary>
    public IList<ReportExposedFilter> Filters { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether to render plain inputs instead of controls that need page scripts, as the
    /// designer preview does.
    /// </summary>
    public bool SimpleControls { get; set; }
}

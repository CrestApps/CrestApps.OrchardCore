using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

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
    /// Gets or sets the report groups a report can be placed in, each followed by the groups nested in it.
    /// </summary>
    public IList<ReportGroupOption> Groups { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the designer may manage report groups.
    /// </summary>
    public bool CanManageGroups { get; set; }

    /// <summary>
    /// Gets or sets the refresh status of the view being designed, or <see langword="null"/> when it has no stored
    /// result.
    /// </summary>
    public ReportViewSnapshotStatus Snapshot { get; set; }

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
    /// Gets or sets the list of the reports the user may edit that were never published, then of the reports the user
    /// can see, with what the user may do with each.
    /// </summary>
    public AdminListContext List { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user can design reports.
    /// </summary>
    public bool CanDesign { get; set; }

    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets which reports are listed: <see cref="ReportDesignListStatus.All"/>, only published ones, or only
    /// the ones never published.
    /// </summary>
    public string Status { get; set; } = ReportDesignListStatus.All;
}

/// <summary>
/// The values of the designed report list's status filter.
/// </summary>
public static class ReportDesignListStatus
{
    /// <summary>
    /// Every report.
    /// </summary>
    public const string All = "";

    /// <summary>
    /// Reports that were published.
    /// </summary>
    public const string Published = "published";

    /// <summary>
    /// Reports that exist only as a draft.
    /// </summary>
    public const string Unpublished = "unpublished";
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

    /// <summary>
    /// Gets or sets a value indicating whether the report was never published, so it has nothing to run yet.
    /// </summary>
    public bool IsDraft { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may make their own copy of the report.
    /// </summary>
    public bool CanClone { get; set; }

    /// <summary>
    /// Gets or sets when an unpublished report was last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the names of the path of the report's group, such as <c>Sales / Regional</c>, or
    /// <see langword="null"/> for a report in no group.
    /// </summary>
    public string GroupPath { get; set; }
}

/// <summary>
/// The view model of the view list.
/// </summary>
public class ReportViewsIndexViewModel
{
    /// <summary>
    /// Gets or sets the list of the views, with whether the user may change each and how its stored result was last
    /// refreshed.
    /// </summary>
    public AdminListContext List { get; set; }
}

/// <summary>
/// One entry of the view list.
/// </summary>
public class ReportViewListEntry
{
    /// <summary>
    /// Gets or sets the view.
    /// </summary>
    public ReportView View { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may change the view.
    /// </summary>
    public bool CanEdit { get; set; }

    /// <summary>
    /// Gets or sets how the stored result of a view refreshed on a schedule was last refreshed, or
    /// <see langword="null"/> for a live view.
    /// </summary>
    public ReportViewSnapshotStatus Snapshot { get; set; }
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
    public IReadOnlyList<ReportExportFormatDescriptor> ExportFormats { get; set; } = [];

    /// <summary>
    /// Gets or sets the function that builds the export URL of a format.
    /// </summary>
    public Func<string, string> ExportUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL the filter form submits to.
    /// </summary>
    public string FormAction { get; set; }

    /// <summary>
    /// Gets or sets the name of the form field that carries the format of an export, when the export URL is the same for
    /// every format, or <see langword="null"/> when <see cref="ExportUrl"/> holds the format.
    /// </summary>
    public string ExportFormatField { get; set; }

    /// <summary>
    /// Gets the fields the filter form submits with the filters, such as the token of a shared link.
    /// </summary>
    public IDictionary<string, string> HiddenFields { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

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

/// <summary>
/// The view model of the designed reports deployment step's editor.
/// </summary>
public class ReportDesignsDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether every report and view is exported.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the reports that can be picked.
    /// </summary>
    public ReportDesignsDeploymentEntryViewModel[] Reports { get; set; } = [];

    /// <summary>
    /// Gets or sets the views that can be picked.
    /// </summary>
    public ReportDesignsDeploymentEntryViewModel[] Views { get; set; } = [];
}

/// <summary>
/// A report or view that the deployment step's editor lists.
/// </summary>
public class ReportDesignsDeploymentEntryViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the report or view.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the title of the report or view.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it is exported.
    /// </summary>
    public bool IsSelected { get; set; }
}

/// <summary>
/// The view model of the report group list.
/// </summary>
public class ReportGroupsIndexViewModel
{
    /// <summary>
    /// Gets or sets the list of the groups, each followed by the groups nested in it.
    /// </summary>
    public AdminListContext List { get; set; }

    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Search { get; set; }
}

/// <summary>
/// One entry of the report group list.
/// </summary>
public class ReportGroupListEntry
{
    /// <summary>
    /// Gets or sets the group.
    /// </summary>
    public ReportGroup Group { get; set; }

    /// <summary>
    /// Gets or sets the names of the group's path, such as <c>Sales / Regional</c>.
    /// </summary>
    public string PathText { get; set; }

    /// <summary>
    /// Gets or sets how deep the group is nested, <c>0</c> for a top-level group.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Gets or sets the number of groups nested directly in the group.
    /// </summary>
    public int GroupCount { get; set; }

    /// <summary>
    /// Gets or sets the number of reports placed in the group.
    /// </summary>
    public int ReportCount { get; set; }
}

/// <summary>
/// The view model of the page that creates or changes a report group.
/// </summary>
public class ReportGroupEditViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether a new group is being created.
    /// </summary>
    [BindNever]
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the group name.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the group it is nested in, or <see langword="null"/> for a top-level group.
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// Gets or sets the display order among the groups that share its parent.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the groups it can be nested in, shown with their path. The group itself and the groups nested in it
    /// are left out.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Parents { get; set; } = [];
}

/// <summary>
/// A report group offered in the designer's group list.
/// </summary>
public class ReportGroupOption
{
    /// <summary>
    /// Gets or sets the group identifier.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the names of the group's path, such as <c>Sales / Regional</c>.
    /// </summary>
    public string Text { get; set; }
}

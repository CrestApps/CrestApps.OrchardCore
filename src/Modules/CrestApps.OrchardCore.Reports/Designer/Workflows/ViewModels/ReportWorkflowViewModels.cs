using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Reports.Designer.Workflows.ViewModels;

/// <summary>
/// The editor of a report event: the reports that start the workflow.
/// </summary>
public class ReportEventViewModel
{
    public IList<string> ReportIds { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Reports { get; set; } = [];
}

/// <summary>
/// The fields every report task has.
/// </summary>
public class ReportTaskViewModel
{
    public string ReportId { get; set; }

    public string ReportIdExpression { get; set; }

    public string Filters { get; set; }

    public string OutputName { get; set; }

    [BindNever]
    public IList<SelectListItem> Reports { get; set; } = [];

    [BindNever]
    public string DefaultOutputName { get; set; }

    [BindNever]
    public bool HasOutput { get; set; } = true;
}

/// <summary>
/// The editor of the Run Report task.
/// </summary>
public class RunReportTaskViewModel : ReportTaskViewModel
{
}

/// <summary>
/// The editor of the Export Report task.
/// </summary>
public class ExportReportTaskViewModel : ReportTaskViewModel
{
    public string Format { get; set; }

    public string MediaFolder { get; set; }

    [BindNever]
    public IList<SelectListItem> Formats { get; set; } = [];
}

/// <summary>
/// The editor of the Email Report task.
/// </summary>
public class EmailReportTaskViewModel : ReportTaskViewModel
{
    public string To { get; set; }

    public string Cc { get; set; }

    public string Bcc { get; set; }

    public string Subject { get; set; }

    public string Body { get; set; }

    public string AttachmentFormat { get; set; }

    public bool IncludeReport { get; set; }

    [BindNever]
    public IList<SelectListItem> Formats { get; set; } = [];
}

/// <summary>
/// The editor of the Create Report Share Link task.
/// </summary>
public class CreateReportShareLinkTaskViewModel : ReportTaskViewModel
{
    public string Recipients { get; set; }

    public string Note { get; set; }

    public int ExpiresAfterDays { get; set; }

    public int MaxUses { get; set; }

    public bool AllowExport { get; set; }

    public bool NotifyByEmail { get; set; }
}

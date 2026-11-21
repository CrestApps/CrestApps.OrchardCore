using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// The filter of the survey reports.
/// </summary>
public class SurveyReportFilterViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the versions read.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the collector.
    /// </summary>
    public string CollectorId { get; set; }

    /// <summary>
    /// Gets or sets the kind of context.
    /// </summary>
    public string ContextKind { get; set; }

    /// <summary>
    /// Gets or sets whether test responses are read.
    /// </summary>
    public bool IncludeTest { get; set; }

    /// <summary>
    /// Gets or sets the choice question the results are segmented by.
    /// </summary>
    public string SegmentQuestionId { get; set; }

    /// <summary>
    /// Gets or sets the question a trend follows.
    /// </summary>
    public string TrendQuestionId { get; set; }

    /// <summary>
    /// Gets or sets the period a trend groups by.
    /// </summary>
    public string TrendPeriod { get; set; }

    /// <summary>
    /// Gets or sets the user audience a participation report checks.
    /// </summary>
    public string AudienceId { get; set; }

    /// <summary>
    /// Gets or sets the report the filter belongs to.
    /// </summary>
    public string ReportName { get; set; }

    /// <summary>
    /// Gets or sets the surveys to pick from.
    /// </summary>
    public IList<SelectListItem> Surveys { get; set; } = [];

    /// <summary>
    /// Gets or sets the version scopes to pick from.
    /// </summary>
    public IList<SelectListItem> Versions { get; set; } = [];

    /// <summary>
    /// Gets or sets the collectors to pick from.
    /// </summary>
    public IList<SelectListItem> Collectors { get; set; } = [];

    /// <summary>
    /// Gets or sets the context kinds to pick from.
    /// </summary>
    public IList<SelectListItem> ContextKinds { get; set; } = [];

    /// <summary>
    /// Gets or sets the choice questions to segment by.
    /// </summary>
    public IList<SelectListItem> SegmentQuestions { get; set; } = [];

    /// <summary>
    /// Gets or sets the questions a trend may follow.
    /// </summary>
    public IList<SelectListItem> TrendQuestions { get; set; } = [];

    /// <summary>
    /// Gets or sets the trend periods.
    /// </summary>
    public IList<SelectListItem> TrendPeriods { get; set; } = [];

    /// <summary>
    /// Gets or sets the audiences to pick from.
    /// </summary>
    public IList<SelectListItem> Audiences { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the report offers the segment.
    /// </summary>
    public bool ShowSegment { get; set; }

    /// <summary>
    /// Gets or sets whether the report offers the trend options.
    /// </summary>
    public bool ShowTrend { get; set; }

    /// <summary>
    /// Gets or sets whether the report offers the audience.
    /// </summary>
    public bool ShowAudience { get; set; }
}

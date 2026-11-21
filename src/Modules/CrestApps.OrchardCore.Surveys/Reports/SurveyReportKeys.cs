namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// The names of the survey report filters, as they are kept in a report filter and read from the address
/// (<c>ReportFilter.SurveyId</c> and so on).
/// </summary>
public static class SurveyReportKeys
{
    /// <summary>
    /// The prefix of the filter's inputs.
    /// </summary>
    public const string Prefix = "SurveyReport";

    /// <summary>
    /// The survey.
    /// </summary>
    public const string SurveyId = "SurveyId";

    /// <summary>
    /// The versions read: <c>published</c>, <c>all</c>, or a version number.
    /// </summary>
    public const string Version = "Version";

    /// <summary>
    /// The collector.
    /// </summary>
    public const string CollectorId = "CollectorId";

    /// <summary>
    /// The kind of context.
    /// </summary>
    public const string ContextKind = "ContextKind";

    /// <summary>
    /// Whether test responses are read.
    /// </summary>
    public const string IncludeTest = "IncludeTest";

    /// <summary>
    /// The choice question the results are segmented by.
    /// </summary>
    public const string SegmentQuestionId = "SegmentQuestionId";

    /// <summary>
    /// The question a trend follows.
    /// </summary>
    public const string TrendQuestionId = "TrendQuestionId";

    /// <summary>
    /// The period a trend groups by: <c>day</c>, <c>week</c> or <c>month</c>.
    /// </summary>
    public const string TrendPeriod = "TrendPeriod";

    /// <summary>
    /// The user audience a participation report checks.
    /// </summary>
    public const string AudienceId = "AudienceId";

    /// <summary>
    /// The version scope that reads the published version.
    /// </summary>
    public const string PublishedVersion = "published";

    /// <summary>
    /// The version scope that reads every version, joined by question id.
    /// </summary>
    public const string AllVersions = "all";
}

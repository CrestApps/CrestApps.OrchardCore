namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// The technical names of the survey reports, which addresses and report widgets use.
/// </summary>
public static class SurveyReportNames
{
    /// <summary>
    /// The category the survey reports are listed under.
    /// </summary>
    public const string Category = "Surveys";

    /// <summary>
    /// Survey results: one section per question.
    /// </summary>
    public const string Results = "survey-results";

    /// <summary>
    /// Survey responses: one row per response.
    /// </summary>
    public const string Responses = "survey-responses";

    /// <summary>
    /// Quiz results: scores and pass rate.
    /// </summary>
    public const string Scores = "survey-scores";

    /// <summary>
    /// Item analysis: difficulty and discrimination of each question.
    /// </summary>
    public const string ItemAnalysis = "survey-item-analysis";

    /// <summary>
    /// Completion funnel: respondents reaching and leaving each page.
    /// </summary>
    public const string Funnel = "survey-funnel";

    /// <summary>
    /// Participation: who in an audience completed and who has not.
    /// </summary>
    public const string Participation = "survey-participation";

    /// <summary>
    /// Satisfaction trend: an answer's average by day, week or month.
    /// </summary>
    public const string Trend = "survey-trend";

    /// <summary>
    /// Survey activity: responses per survey and responses waiting for a grader.
    /// </summary>
    public const string Activity = "survey-activity";

    /// <summary>
    /// Gets whether a report is a survey report that reads one survey.
    /// </summary>
    /// <param name="reportName">The report name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public static bool IsSurveyReport(string reportName)
        => reportName is Results or Responses or Scores or ItemAnalysis or Funnel or Participation or Trend;
}

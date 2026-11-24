using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// The one test of whether a survey is closed to a respondent. A survey closes when staff close or archive it, when its
/// closing date passes (an accommodation's closing date replaces the survey's), and when the link it is answered through
/// is closed or its closing date passes. A closed survey takes no new answers: it starts nothing, saves nothing, reopens
/// nothing and completes nothing, and a response left in progress is submitted as it was saved before the close.
/// </summary>
public static class SurveyAvailability
{
    /// <summary>
    /// Gets when the survey closes for a respondent: the accommodation's closing date, else the survey's, and the link's
    /// closing date when that comes first.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="surveyOverride">The accommodation that applies, or <see langword="null"/>.</param>
    /// <param name="collector">The link, or <see langword="null"/>.</param>
    /// <returns>The time, in UTC, or <see langword="null"/> when it has no closing date.</returns>
    public static DateTime? ClosesUtc(Survey survey, SurveyOverride surveyOverride, SurveyCollector collector)
    {
        var closes = surveyOverride?.ClosesUtc ?? survey?.Access?.ClosesUtc;

        if (collector?.ClosesUtc is { } collectorCloses && (closes is null || collectorCloses < closes))
        {
            closes = collectorCloses;
        }

        return closes;
    }

    /// <summary>
    /// Gets the grace, in seconds, that a respondent's write in flight at their deadline or at the closing date gets
    /// (<see cref="SurveyTimingSettings.GraceSeconds"/>, 5 by default): a deadline is often clamped to the closing date,
    /// so both give a last page posted just before them the same time to arrive. A close by staff has no grace.
    /// </summary>
    /// <param name="definition">The definition of the response's version, or <see langword="null"/>.</param>
    /// <returns>The grace.</returns>
    public static int GraceSeconds(SurveyDefinition definition)
        => Math.Max(definition?.Timing?.GraceSeconds ?? 5, 0);

    /// <summary>
    /// Gets whether staff closed the survey or the link: its status, whatever the dates say.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="collector">The link, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it is closed.</returns>
    public static bool IsClosedByStatus(Survey survey, SurveyCollector collector)
        => survey?.Status is SurveyStatus.Closed or SurveyStatus.Archived || collector?.Status == CollectorStatus.Closed;

    /// <summary>
    /// Gets whether the survey is closed to a respondent now.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="surveyOverride">The accommodation that applies, or <see langword="null"/>.</param>
    /// <param name="collector">The link, or <see langword="null"/>.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns><see langword="true"/> when it is closed.</returns>
    public static bool IsClosed(Survey survey, SurveyOverride surveyOverride, SurveyCollector collector, DateTime utcNow)
        => IsClosedByStatus(survey, collector) || ClosesUtc(survey, surveyOverride, collector) is { } closes && utcNow >= closes;
}

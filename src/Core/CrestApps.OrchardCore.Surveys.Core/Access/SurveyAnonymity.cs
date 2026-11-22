using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// The rules every output of an anonymous survey follows: the minimum group size, with the site's floor, and times shown
/// by the day only.
/// </summary>
public static class SurveyAnonymity
{
    /// <summary>
    /// The id an anonymous response keeps in place of the id of a context that can point to one person.
    /// </summary>
    public const string HiddenContextId = "anonymous";

    /// <summary>
    /// Gets whether a survey is anonymous.
    /// </summary>
    /// <param name="survey">The survey, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it records nobody.</returns>
    public static bool IsAnonymous(Survey survey)
        => survey?.Access?.Anonymity == AnonymityMode.Anonymous;

    /// <summary>
    /// Gets the floor of the minimum group size the site sets, at least 1.
    /// </summary>
    /// <param name="settings">The site settings, or <see langword="null"/>.</param>
    /// <returns>The floor.</returns>
    public static int Floor(SurveySettings settings)
        => Math.Max(1, settings?.MinimumGroupSizeFloor ?? SurveySettings.DefaultMinimumGroupSizeFloor);

    /// <summary>
    /// Gets the fewest responses an output of a survey may show: the survey's own minimum, never below the site's floor,
    /// or 0 for an identified survey.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="settings">The site settings, or <see langword="null"/>.</param>
    /// <returns>The minimum, or 0 when there is none.</returns>
    public static int MinimumGroupSize(Survey survey, SurveySettings settings)
        => IsAnonymous(survey) ? Math.Max(survey.Access?.MinimumGroupSize ?? 0, Floor(settings)) : 0;

    /// <summary>
    /// Gets whether a group of responses is too small to show.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="settings">The site settings, or <see langword="null"/>.</param>
    /// <param name="count">How many responses the group holds.</param>
    /// <returns><see langword="true"/> when the group must be hidden.</returns>
    public static bool IsTooSmall(Survey survey, SurveySettings settings, long count)
    {
        var minimum = MinimumGroupSize(survey, settings);

        return minimum > 0 && count < minimum;
    }

    /// <summary>
    /// Gets the context a response of a survey keeps: on an anonymous survey, a context whose kind can point to one person
    /// (an enrollment, an invitation's recipient, or any kind not registered as shared) keeps its kind only.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="context">The context asked for, or <see langword="null"/>.</param>
    /// <param name="kinds">The registered context kinds, or <see langword="null"/>.</param>
    /// <returns>The context to keep.</returns>
    public static RecordRef? StoredContext(Survey survey, RecordRef? context, SurveyContextKindOptions kinds)
    {
        if (context is not { IsEmpty: false } value || !IsAnonymous(survey))
        {
            return context;
        }

        return kinds?.Kinds.TryGetValue(value.Kind, out var kind) == true && kind is { IdentifiesRespondent: false }
            ? value
            : new RecordRef(value.Kind, HiddenContextId);
    }

    /// <summary>
    /// Gets a time of an anonymous response shown by the day only: the start of its day in a time zone, as UTC.
    /// </summary>
    /// <param name="utc">The time, in UTC.</param>
    /// <param name="timeZone">The time zone the day is read in, or <see langword="null"/> for UTC.</param>
    /// <returns>The start of the day, in UTC.</returns>
    public static DateTime StartOfDayUtc(DateTime utc, TimeZoneInfo timeZone)
    {
        var zone = timeZone ?? TimeZoneInfo.Utc;
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);

        // A midnight a daylight-saving change skips starts the day when the clocks move forward.
        return CalendarTime.ToUtc(local.Date, zone.Id);
    }
}

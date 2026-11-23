using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// The one rule every output that lists the responses of an anonymous survey follows (the responses list, the Survey
/// Responses report, the data sets, the API and grading), so they can't drift apart:
/// <list type="bullet">
/// <item>nothing shows until the completed, non-test responses the output's narrowing filters select reach the
/// survey's minimum group size;</item>
/// <item>then each status whose non-test responses are fewer than the minimum is left out, so filtering to (or
/// reading next to the completed rows) the one abandoned, screened-out or in-progress response can't single out a
/// person;</item>
/// <item>an output that lists only some statuses shows nothing when every status it lists is left out.</item>
/// </list>
/// Test responses never count and are never left out on their own: they are staff previews.
/// </summary>
public static class SurveyGroupGate
{
    /// <summary>
    /// Gets every status a response can have.
    /// </summary>
    public static IReadOnlyList<SurveyResponseStatus> AllStatuses { get; } = Enum.GetValues<SurveyResponseStatus>();

    /// <summary>
    /// Decides what an output may show.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="settings">The site settings, or <see langword="null"/>.</param>
    /// <param name="shown">The statuses the output lists, or empty for every status.</param>
    /// <param name="countReal">Counts the non-test responses of a status the output's narrowing filters (but not its
    /// status filter) select.</param>
    /// <returns>The decision.</returns>
    public static async Task<SurveyGroupDecision> DecideAsync(
        Survey survey,
        SurveySettings settings,
        IReadOnlyCollection<SurveyResponseStatus> shown,
        Func<SurveyResponseStatus, Task<long>> countReal)
    {
        ArgumentNullException.ThrowIfNull(countReal);

        var minimum = SurveyAnonymity.MinimumGroupSize(survey, settings);

        if (minimum == 0)
        {
            return SurveyGroupDecision.Open;
        }

        var counts = new Dictionary<SurveyResponseStatus, long>();

        foreach (var status in shown is { Count: > 0 } ? shown.Append(SurveyResponseStatus.Completed).Distinct() : AllStatuses)
        {
            counts[status] = await countReal(status);
        }

        return Decide(minimum, shown, counts);
    }

    /// <summary>
    /// Decides what an output may show from the counts of its non-test responses by status.
    /// </summary>
    /// <param name="minimum">The minimum group size, or 0 for none.</param>
    /// <param name="shown">The statuses the output lists, or empty for every status.</param>
    /// <param name="counts">The non-test responses of each status; a status missing counts 0.</param>
    /// <returns>The decision.</returns>
    public static SurveyGroupDecision Decide(int minimum, IReadOnlyCollection<SurveyResponseStatus> shown, IReadOnlyDictionary<SurveyResponseStatus, long> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);

        if (minimum <= 0)
        {
            return SurveyGroupDecision.Open;
        }

        long CountOf(SurveyResponseStatus status)
            => counts.GetValueOrDefault(status);

        if (CountOf(SurveyResponseStatus.Completed) < minimum)
        {
            return new SurveyGroupDecision { MinimumGroupSize = minimum, HidesAll = true };
        }

        var listed = shown is { Count: > 0 } ? shown.Distinct().ToList() : AllStatuses.ToList();
        var hidden = listed.Where(status => !ShowsStatus(minimum, CountOf(SurveyResponseStatus.Completed), CountOf(status))).ToList();

        // A filter to statuses that are all too small shows nothing (with the notice), not just an empty page.
        var hidesAll = shown is { Count: > 0 } && hidden.Count > 0 && listed.All(status => hidden.Contains(status) || CountOf(status) == 0);

        return new SurveyGroupDecision
        {
            MinimumGroupSize = minimum,
            HidesAll = hidesAll,
            HiddenStatuses = hidden,
        };
    }

    /// <summary>
    /// Gets whether the responses of one status may show: the completed responses reach the minimum, and the status
    /// has none or at least the minimum. The counts only grow while an output reads, so a reader that streams can
    /// release a status as soon as this turns true.
    /// </summary>
    /// <param name="minimum">The minimum group size, or 0 for none.</param>
    /// <param name="completed">The completed, non-test responses.</param>
    /// <param name="count">The non-test responses of the status.</param>
    /// <returns><see langword="true"/> when they may show.</returns>
    public static bool ShowsStatus(int minimum, long completed, long count)
        => minimum <= 0 || (completed >= minimum && (count == 0 || count >= minimum));
}

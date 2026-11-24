using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// The small-group rule of the attributes each row of an anonymous survey prints: the collector, the version and the
/// context kind, decided together on their joint key (<see cref="SurveyResponseGroup"/>). A response's three attributes
/// show only when at least the minimum group size of the survey's completed, non-test responses share all three; in a
/// smaller group all three show as "Other" (the version is left blank). Deciding them one at a time would let a filter
/// on one attribute and a read of another single out a response (collector A and version 2 each large, but only one
/// person answered version 2 through A). When all the smaller groups hold fewer than the minimum together, their
/// responses are not shown at all, since "Other" would then still point to a few people (a link sent to one person).
/// The counts are of the whole survey, so no filter can narrow them. Details, the Grade page, the grading queues, the
/// admin list, the Survey Responses report, the data sets, the API, and the webhook and workflow payloads all decide
/// through this one rule.
/// </summary>
public sealed class SurveyAnonymousAttributes
{
    private readonly IReadOnlyDictionary<SurveyResponseGroup, long> _counts;

    private SurveyAnonymousAttributes(int minimum, IReadOnlyDictionary<SurveyResponseGroup, long> counts)
    {
        MinimumGroupSize = minimum;
        _counts = counts ?? new Dictionary<SurveyResponseGroup, long>();
        OtherIsLarge = minimum > 0 && _counts.Values.Where(count => count < minimum).Sum() >= minimum;
    }

    /// <summary>
    /// Gets the rule of an identified survey: every attribute shows.
    /// </summary>
    public static SurveyAnonymousAttributes Open { get; } = new(0, null);

    /// <summary>
    /// Gets the minimum group size, or 0 when every attribute shows.
    /// </summary>
    public int MinimumGroupSize { get; }

    /// <summary>
    /// Gets whether the groups too small to show hold at least the minimum together, so "Other" hides their responses
    /// and none of them is left out.
    /// </summary>
    public bool OtherIsLarge { get; }

    /// <summary>
    /// Makes the rule from the counts of a survey's completed, non-test responses by their joint key.
    /// </summary>
    /// <param name="minimum">The minimum group size, or 0 for none.</param>
    /// <param name="counts">The counts by joint key (see <see cref="SurveyResponseGroup.Create"/>).</param>
    /// <returns>The rule.</returns>
    public static SurveyAnonymousAttributes Create(int minimum, IReadOnlyDictionary<SurveyResponseGroup, long> counts)
        => minimum <= 0 ? Open : new SurveyAnonymousAttributes(minimum, counts);

    /// <summary>
    /// Decides what may show of one response.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns>The decision.</returns>
    public SurveyRowAttributes Of(SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (MinimumGroupSize <= 0)
        {
            return SurveyRowAttributes.All;
        }

        var shows = Shows(SurveyResponseGroup.Of(response));

        // A test response is a staff preview: it is never hidden on its own, and still masks a small group's values.
        return new SurveyRowAttributes(!response.IsTest && !shows && !OtherIsLarge, shows, shows, shows);
    }

    /// <summary>
    /// Gets whether the real responses of a group are left out of every output: the group is too small to show, and
    /// the groups too small to show hold too few responses together to hide in.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    public bool Hides(SurveyResponseGroup group)
        => MinimumGroupSize > 0 && !OtherIsLarge && !Shows(group);

    /// <summary>
    /// Gets whether the attributes of a group show as themselves: no minimum, or at least the minimum of the survey's
    /// completed, non-test responses share the group. A group that doesn't show is masked as "Other" or hidden.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns><see langword="true"/> when it shows.</returns>
    public bool Shows(SurveyResponseGroup group)
        => MinimumGroupSize <= 0 || _counts.GetValueOrDefault(group) >= MinimumGroupSize;
}

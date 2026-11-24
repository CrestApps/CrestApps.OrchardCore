using System.Globalization;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// The small-group rule of the attributes each row of an anonymous survey prints: the collector, the version and the
/// context kind. Each is a group of its own, like the status in <see cref="SurveyGroupGate"/>: a value shows only when
/// at least the minimum group size of the survey's completed, non-test responses share it. A smaller group's values
/// show as "Other" (the version is left blank), and when all the smaller groups of an attribute hold fewer than the
/// minimum together, their responses are not shown at all, since "Other" would then still point to a few people (a
/// link sent to one person). The counts are of the whole survey, so no filter can narrow them.
/// </summary>
public sealed class SurveyAnonymousAttributes
{
    private readonly Group _collectors;
    private readonly Group _versions;
    private readonly Group _contextKinds;

    private SurveyAnonymousAttributes(int minimum, Group collectors, Group versions, Group contextKinds)
    {
        MinimumGroupSize = minimum;
        _collectors = collectors;
        _versions = versions;
        _contextKinds = contextKinds;
    }

    /// <summary>
    /// Gets the rule of an identified survey: every attribute shows.
    /// </summary>
    public static SurveyAnonymousAttributes Open { get; } = new(0, null, null, null);

    /// <summary>
    /// Gets the minimum group size, or 0 when every attribute shows.
    /// </summary>
    public int MinimumGroupSize { get; }

    /// <summary>
    /// Makes the rule from the counts of a survey's completed, non-test responses.
    /// </summary>
    /// <param name="minimum">The minimum group size, or 0 for none.</param>
    /// <param name="collectors">The counts by collector id (empty for none).</param>
    /// <param name="versions">The counts by version number, as invariant text.</param>
    /// <param name="contextKinds">The counts by context kind (empty for none).</param>
    /// <returns>The rule.</returns>
    public static SurveyAnonymousAttributes Create(
        int minimum,
        IReadOnlyDictionary<string, long> collectors,
        IReadOnlyDictionary<string, long> versions,
        IReadOnlyDictionary<string, long> contextKinds)
        => minimum <= 0
            ? Open
            : new SurveyAnonymousAttributes(minimum, new Group(minimum, collectors), new Group(minimum, versions), new Group(minimum, contextKinds));

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

        var collector = _collectors.Shows(response.CollectorId);
        var version = _versions.Shows(response.VersionNumber.ToString(CultureInfo.InvariantCulture));
        var kind = _contextKinds.Shows(response.Context?.Kind);

        // A test response is a staff preview: it is never hidden on its own, and still masks small groups' values.
        var hidden = !response.IsTest
            && ((!collector && !_collectors.OtherIsLarge) || (!version && !_versions.OtherIsLarge) || (!kind && !_contextKinds.OtherIsLarge));

        return new SurveyRowAttributes(hidden, collector, version, kind);
    }

    private sealed class Group
    {
        private readonly int _minimum;
        private readonly IReadOnlyDictionary<string, long> _counts;

        public Group(int minimum, IReadOnlyDictionary<string, long> counts)
        {
            _minimum = minimum;
            _counts = counts ?? new Dictionary<string, long>(StringComparer.Ordinal);
            OtherIsLarge = _counts.Values.Where(count => count < minimum).Sum() >= minimum;
        }

        // Whether the groups too small to show hold at least the minimum together, so "Other" hides them.
        public bool OtherIsLarge { get; }

        public bool Shows(string value)
            => _counts.GetValueOrDefault(value ?? string.Empty) >= _minimum;
    }
}

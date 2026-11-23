using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// What an output of a survey may show of its responses under the minimum group size of an anonymous survey: nothing,
/// or every status but the ones too few responses share. See <see cref="SurveyGroupGate"/>.
/// </summary>
public sealed class SurveyGroupDecision
{
    /// <summary>
    /// Gets the decision of an output with no minimum: everything shows.
    /// </summary>
    public static SurveyGroupDecision Open { get; } = new();

    /// <summary>
    /// Gets the fewest responses a group may show, or 0 when there is no minimum.
    /// </summary>
    public int MinimumGroupSize { get; init; }

    /// <summary>
    /// Gets whether the output shows no response at all.
    /// </summary>
    public bool HidesAll { get; init; }

    /// <summary>
    /// Gets the statuses whose real (non-test) responses are too few to show; an output leaves them out.
    /// </summary>
    public IReadOnlyList<SurveyResponseStatus> HiddenStatuses { get; init; } = [];

    /// <summary>
    /// Gets whether a response may show: never when everything is hidden, and a real response of a hidden status
    /// never. Test responses are previews by staff, not people, so only <see cref="HidesAll"/> hides them.
    /// </summary>
    /// <param name="status">The response's status.</param>
    /// <param name="isTest">Whether it is a test response.</param>
    /// <returns><see langword="true"/> when it may show.</returns>
    public bool Shows(SurveyResponseStatus status, bool isTest)
        => !HidesAll && (isTest || !HiddenStatuses.Contains(status));
}

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// What an output of an anonymous survey may show of one response's own attributes (see
/// <see cref="SurveyAnonymousAttributes"/>).
/// </summary>
/// <param name="Hidden">Whether the response must not be shown at all: one of its attributes is in a group too small
/// to show, and the groups too small to show hold too few responses together to hide in.</param>
/// <param name="ShowsCollector">Whether its collector may show; otherwise it shows as "Other".</param>
/// <param name="ShowsVersion">Whether its version number may show; otherwise it is left blank.</param>
/// <param name="ShowsContextKind">Whether its context kind may show; otherwise it shows as "Other".</param>
public readonly record struct SurveyRowAttributes(bool Hidden, bool ShowsCollector, bool ShowsVersion, bool ShowsContextKind)
{
    /// <summary>
    /// Gets the attributes of a response that shows everything.
    /// </summary>
    public static SurveyRowAttributes All { get; } = new(false, true, true, true);
}

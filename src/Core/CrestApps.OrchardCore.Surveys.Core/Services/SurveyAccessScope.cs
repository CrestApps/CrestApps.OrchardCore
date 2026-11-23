namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// The surveys a user may do an operation on: every survey (the global permission), or a set of survey ids.
/// </summary>
public sealed class SurveyAccessScope
{
    /// <summary>
    /// Gets a scope with no survey.
    /// </summary>
    public static SurveyAccessScope None { get; } = new() { SurveyIds = new HashSet<string>(StringComparer.Ordinal) };

    /// <summary>
    /// Gets a scope with every survey.
    /// </summary>
    public static SurveyAccessScope Everything { get; } = new() { All = true, SurveyIds = new HashSet<string>(StringComparer.Ordinal) };

    /// <summary>
    /// Gets a value indicating whether every survey is in the scope, so no query needs a survey filter.
    /// </summary>
    public bool All { get; init; }

    /// <summary>
    /// Gets the ids of the surveys in the scope when <see cref="All"/> is <see langword="false"/>.
    /// </summary>
    public IReadOnlySet<string> SurveyIds { get; init; }

    /// <summary>
    /// Gets a value indicating whether no survey is in the scope.
    /// </summary>
    public bool IsEmpty => !All && SurveyIds.Count == 0;

    /// <summary>
    /// Gets whether a survey is in the scope.
    /// </summary>
    /// <param name="surveyId">The survey id.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public bool Contains(string surveyId)
        => !string.IsNullOrEmpty(surveyId) && (All || SurveyIds.Contains(surveyId));
}

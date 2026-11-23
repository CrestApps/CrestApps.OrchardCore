namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// What the current page shows with the answers given: hidden questions and choices, and piped texts.
/// </summary>
public sealed class SurveyEvaluateResult : SurveyResponseResult
{
    /// <summary>
    /// Gets or sets the ids of the questions hidden now.
    /// </summary>
    public List<string> HiddenQuestionIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the ids of the choices hidden now, by question id.
    /// </summary>
    public Dictionary<string, List<string>> HiddenChoiceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the piped texts by key (<c>title:{questionId}</c>, <c>description:{questionId}</c>,
    /// <c>choice:{questionId}:{choiceId}</c>).
    /// </summary>
    public Dictionary<string, string> Texts { get; set; } = [];

    /// <summary>
    /// Gets or sets the values of calculated variables.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = [];

    /// <summary>
    /// Gets or sets the errors by question id that saving these answers would get: with immediate feedback, the answers
    /// that would take an answer already checked off the respondent's path or hide it, which a save or a page post refuses.
    /// </summary>
    public Dictionary<string, List<string>> Errors { get; set; } = [];
}

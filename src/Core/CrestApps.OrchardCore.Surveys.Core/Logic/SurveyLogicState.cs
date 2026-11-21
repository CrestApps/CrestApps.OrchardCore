namespace CrestApps.OrchardCore.Surveys.Core.Logic;

/// <summary>
/// What the logic of a survey decides for one response at one moment: which pages, questions and choices are hidden,
/// which questions are disabled and which are required.
/// </summary>
public sealed class SurveyLogicState
{
    /// <summary>
    /// Gets the ids of the hidden pages.
    /// </summary>
    public HashSet<string> HiddenPageIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the ids of the hidden questions (a question on a hidden page is hidden).
    /// </summary>
    public HashSet<string> HiddenQuestionIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the ids of the hidden choices, by question id.
    /// </summary>
    public Dictionary<string, HashSet<string>> HiddenChoiceIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the ids of the questions that cannot be answered now.
    /// </summary>
    public HashSet<string> DisabledQuestionIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the ids of the questions that need an answer now.
    /// </summary>
    public HashSet<string> RequiredQuestionIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets whether a choice is hidden.
    /// </summary>
    /// <param name="questionId">The question.</param>
    /// <param name="choiceId">The choice.</param>
    /// <returns><see langword="true"/> when it is hidden.</returns>
    public bool IsChoiceHidden(string questionId, string choiceId)
        => HiddenChoiceIds.TryGetValue(questionId, out var hidden) && hidden.Contains(choiceId);
}

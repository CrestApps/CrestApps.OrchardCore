using CrestApps.OrchardCore.Surveys.Controls;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.ChoiceBlanks;

/// <summary>
/// The settings of a choice blanks question.
/// </summary>
public sealed class ChoiceBlanksSettings
{
    /// <summary>
    /// Gets or sets the text with blanks written as <c>[[right word]]</c>. It holds the key, so respondents only see its
    /// segments and the pool of words.
    /// </summary>
    [SurveyKey]
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets wrong words added to the pool.
    /// </summary>
    public List<string> Distractors { get; set; } = [];

    /// <summary>
    /// Gets or sets how words are picked.
    /// </summary>
    public ChoiceBlanksDisplay Display { get; set; }
}

using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// A text whose blanks are filled from a list of choices: the right word of each blank is written in the text as
/// <c>[[word]]</c>, and the distractors are choices no blank takes. Drag into text and gapselect questions both use it;
/// they differ only in how the learner fills a blank.
/// </summary>
public sealed class ChoiceBlanksQuestionPart : ContentPart
{
    /// <summary>
    /// Gets or sets the text with its blanks.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the choices no blank takes.
    /// </summary>
    public IList<string> Distractors { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the choices are shown in a random order in each attempt.
    /// </summary>
    public bool ShuffleChoices { get; set; } = true;
}

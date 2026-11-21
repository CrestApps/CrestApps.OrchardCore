namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// An option of a choice question.
/// </summary>
public sealed class ChoiceOption
{
    /// <summary>
    /// Gets or sets the stable id of the option.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the text of the option.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets whether the option is correct.
    /// </summary>
    public bool Correct { get; set; }

    /// <summary>
    /// Gets or sets the feedback shown in the review when the option was chosen.
    /// </summary>
    public string Feedback { get; set; }
}

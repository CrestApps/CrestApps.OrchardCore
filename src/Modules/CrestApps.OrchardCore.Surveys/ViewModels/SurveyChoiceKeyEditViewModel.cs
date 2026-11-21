namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The key of one choice: right or not, its points and its feedback.
/// </summary>
public class SurveyChoiceKeyEditViewModel
{
    /// <summary>
    /// Gets or sets the choice id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the choice text.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets whether the choice is right.
    /// </summary>
    public bool Correct { get; set; }

    /// <summary>
    /// Gets or sets the points of the choice, or <see langword="null"/>.
    /// </summary>
    public decimal? Points { get; set; }

    /// <summary>
    /// Gets or sets the feedback of the choice.
    /// </summary>
    public string Feedback { get; set; }
}

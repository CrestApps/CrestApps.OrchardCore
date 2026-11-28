using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The model of the <c>SurveyQuestion_Respond</c> wrapper shape: number, title, description, required mark, errors,
/// feedback, and the control shape of the question's type.
/// </summary>
public class SurveyQuestionRespondViewModel
{
    /// <summary>
    /// Gets or sets the respondent view of the question.
    /// </summary>
    public SurveyQuestionView Question { get; set; }

    /// <summary>
    /// Gets or sets the control shape.
    /// </summary>
    public IShape Control { get; set; }

    /// <summary>
    /// Gets or sets the errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets the feedback, or <see langword="null"/>.
    /// </summary>
    public SurveyAnswerFeedback Feedback { get; set; }

    /// <summary>
    /// Gets or sets whether the question is hidden now (the runner shows it when answers change).
    /// </summary>
    public bool Hidden { get; set; }

    /// <summary>
    /// Gets or sets whether the question can be checked with immediate feedback.
    /// </summary>
    public bool CanCheck { get; set; }

    /// <summary>
    /// Gets or sets whether the answer was checked with immediate feedback and is locked: the runner script never shows
    /// or hides it as other answers change, because the server refuses a change that would hide it.
    /// </summary>
    public bool Locked { get; set; }

    /// <summary>
    /// Gets or sets whether the question is answerable.
    /// </summary>
    public bool Answerable { get; set; }

    /// <summary>
    /// Gets or sets the display type: Respond or Review.
    /// </summary>
    public string DisplayType { get; set; } = "Respond";
}

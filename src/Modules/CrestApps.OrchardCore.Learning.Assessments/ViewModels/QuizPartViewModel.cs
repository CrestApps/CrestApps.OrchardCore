using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.Assessments.ViewModels;

/// <summary>
/// The editor of a quiz: a summary of the survey the quiz owns, links to its pages, and the grading method.
/// </summary>
public class QuizPartViewModel
{
    /// <summary>
    /// Gets or sets the survey, or <see langword="null"/> before the quiz is first saved.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets how many questions the draft has (pool draws counted by their draw count).
    /// </summary>
    public int QuestionCount { get; set; }

    /// <summary>
    /// Gets or sets how the attempts combine into the quiz grade.
    /// </summary>
    public GradingMethod GradingMethod { get; set; }

    /// <summary>
    /// Gets or sets the grading methods to pick from.
    /// </summary>
    public IList<SelectListItem> GradingMethods { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the quiz completes "When the learner passes" but its survey has neither a pass mark nor a
    /// score band that passes or fails, so any graded attempt completes it, neither passed nor failed.
    /// </summary>
    public bool NoPassDecision { get; set; }
}

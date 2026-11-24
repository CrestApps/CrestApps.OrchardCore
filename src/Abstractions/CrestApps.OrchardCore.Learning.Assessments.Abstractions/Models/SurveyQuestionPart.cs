using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// The options of a survey question. Every answer is accepted: at zero points the question is ungraded, and with points
/// any answer earns them.
/// </summary>
public sealed class SurveyQuestionPart : ContentPart
{
    /// <summary>
    /// Gets or sets the options. Their <see cref="ChoiceOption.Correct"/> is not used.
    /// </summary>
    public IList<ChoiceOption> Options { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the learner may choose several options.
    /// </summary>
    public bool Multiple { get; set; }
}

using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The survey and draft the designer is editing in the current request, so the question and definition editors know
/// the questions a rule may read and whether scoring is on.
/// </summary>
public sealed class SurveyDesignerContext
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the definition edited (the draft, or a bank question's scratch definition).
    /// </summary>
    public SurveyDefinition Definition { get; set; }

    /// <summary>
    /// Gets or sets whether scoring editors show even when the definition does not score (question banks).
    /// </summary>
    public bool AlwaysShowScoring { get; set; }
}

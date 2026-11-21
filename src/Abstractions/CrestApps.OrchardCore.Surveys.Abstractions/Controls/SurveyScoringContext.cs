using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// The survey-wide rules a scorer applies.
/// </summary>
public sealed class SurveyScoringContext
{
    /// <summary>
    /// Gets or sets the definition.
    /// </summary>
    public SurveyDefinition Definition { get; set; }

    /// <summary>
    /// Gets or sets the scoring settings.
    /// </summary>
    public SurveyScoringSettings Settings { get; set; } = new();

    /// <summary>
    /// Gets or sets the choice ids the respondent saw, in order, or empty.
    /// </summary>
    public IReadOnlyList<string> ShownChoiceIds { get; set; } = [];

    /// <summary>
    /// Gets the points of a question: its own, or the survey default.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <returns>The points.</returns>
    public decimal PointsOf(SurveyQuestion question)
        => question?.Scoring?.Points ?? Settings?.DefaultPoints ?? 1;
}

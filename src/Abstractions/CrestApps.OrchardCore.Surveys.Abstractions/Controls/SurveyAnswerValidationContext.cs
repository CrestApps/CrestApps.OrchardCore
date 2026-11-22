using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// The state of checking one answer.
/// </summary>
public sealed class SurveyAnswerValidationContext
{
    /// <summary>
    /// Gets or sets the question.
    /// </summary>
    public SurveyQuestion Question { get; set; }

    /// <summary>
    /// Gets or sets the answer, or <see langword="null"/>.
    /// </summary>
    public SurveyAnswer Answer { get; set; }

    /// <summary>
    /// Gets or sets whether an answer is required now (Required, or RequiredWhen when set).
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the definition.
    /// </summary>
    public SurveyDefinition Definition { get; set; }

    /// <summary>
    /// Gets or sets the response.
    /// </summary>
    public SurveyResponse Response { get; set; }

    /// <summary>
    /// Gets or sets the choice ids the respondent may pick, in plan order.
    /// </summary>
    public IReadOnlyList<string> AllowedChoiceIds { get; set; } = [];

    /// <summary>
    /// Gets the errors found.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets or sets the time budget the pattern rules share with the other answers checked in the same request, or
    /// <see langword="null"/> to give this answer a budget of its own.
    /// </summary>
    public SurveyPatternBudget PatternBudget { get; set; }
}

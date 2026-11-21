namespace CrestApps.OrchardCore.Surveys.Core.Formulas;

/// <summary>
/// What a survey formula field reads.
/// </summary>
public enum SurveyFormulaFieldKind
{
    /// <summary>
    /// An answer, or one row of it.
    /// </summary>
    Question,

    /// <summary>
    /// A variable.
    /// </summary>
    Variable,

    /// <summary>
    /// The total score.
    /// </summary>
    Score,

    /// <summary>
    /// The score as a percent.
    /// </summary>
    Percent,

    /// <summary>
    /// The score of a category.
    /// </summary>
    CategoryScore,
}

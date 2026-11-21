namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// The score of one answer.
/// </summary>
public sealed class SurveyScoreResult
{
    /// <summary>
    /// Gets or sets the points, or <see langword="null"/> when a grader must score the answer.
    /// </summary>
    public decimal? Points { get; set; }

    /// <summary>
    /// Gets or sets the most points possible.
    /// </summary>
    public decimal MaxPoints { get; set; }

    /// <summary>
    /// Gets or sets whether the answer is fully right, or <see langword="null"/> when that does not apply.
    /// </summary>
    public bool? Correct { get; set; }

    /// <summary>
    /// Makes a result a grader must score.
    /// </summary>
    /// <param name="maxPoints">The most points possible.</param>
    /// <returns>The result.</returns>
    public static SurveyScoreResult Manual(decimal maxPoints)
        => new() { MaxPoints = maxPoints };

    /// <summary>
    /// Makes a scored result.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <param name="maxPoints">The most points possible.</param>
    /// <returns>The result.</returns>
    public static SurveyScoreResult Of(decimal points, decimal maxPoints)
        => new() { Points = points, MaxPoints = maxPoints, Correct = maxPoints > 0 ? points >= maxPoints : null };
}

using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// More time, more attempts or another window for one person on one quiz, with a reason. It is also the accessibility
/// accommodation.
/// </summary>
public sealed class AssessmentOverride : CatalogItem
{
    /// <summary>
    /// Gets or sets the learner.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the quiz.
    /// </summary>
    public string QuizId { get; set; }

    /// <summary>
    /// Gets or sets the extra minutes of each attempt.
    /// </summary>
    public int ExtraMinutes { get; set; }

    /// <summary>
    /// Gets or sets the extra attempts.
    /// </summary>
    public int ExtraAttempts { get; set; }

    /// <summary>
    /// Gets or sets another opening time, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? OpensUtc { get; set; }

    /// <summary>
    /// Gets or sets another closing time, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? ClosesUtc { get; set; }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets who gave it.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets when, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

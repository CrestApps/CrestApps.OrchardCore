using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// A quiz course item: its questions, draw rules, time limit, attempts, pass mark, grading method, window and review
/// policy.
/// </summary>
public sealed class QuizPart : ContentPart
{
    /// <summary>
    /// Gets or sets the questions every attempt includes, in order.
    /// </summary>
    public IList<string> QuestionIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the rules that draw questions at random.
    /// </summary>
    public IList<QuizDrawRule> DrawRules { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the questions are shuffled in each attempt.
    /// </summary>
    public bool ShuffleQuestions { get; set; }

    /// <summary>
    /// Gets or sets the time limit in minutes, or <see langword="null"/> for none.
    /// </summary>
    public int? TimeLimitMinutes { get; set; }

    /// <summary>
    /// Gets or sets how many attempts a learner has, or <see langword="null"/> for no limit.
    /// </summary>
    public int? MaxAttempts { get; set; } = 1;

    /// <summary>
    /// Gets or sets how many minutes a learner waits between attempts.
    /// </summary>
    public int DelayMinutes { get; set; }

    /// <summary>
    /// Gets or sets the pass mark in percent, or <see langword="null"/> to use the item pass mark.
    /// </summary>
    public decimal? PassMark { get; set; }

    /// <summary>
    /// Gets or sets which attempt sets the score.
    /// </summary>
    public GradingMethod GradingMethod { get; set; }

    /// <summary>
    /// Gets or sets how a learner moves through the questions.
    /// </summary>
    public QuizNavigation Navigation { get; set; }

    /// <summary>
    /// Gets or sets when the quiz opens, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? OpensUtc { get; set; }

    /// <summary>
    /// Gets or sets when the quiz closes, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? ClosesUtc { get; set; }

    /// <summary>
    /// Gets or sets the hash of the access code a learner enters to start, or <see langword="null"/>.
    /// </summary>
    public string AccessCodeHash { get; set; }

    /// <summary>
    /// Gets or sets the statement a learner confirms before starting, or <see langword="null"/>.
    /// </summary>
    public string HonorStatement { get; set; }

    /// <summary>
    /// Gets or sets when a learner may see the answers and feedback.
    /// </summary>
    public ReviewPolicy ReviewPolicy { get; set; } = ReviewPolicy.AfterGraded;
}

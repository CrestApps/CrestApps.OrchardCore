using CrestApps.OrchardCore.Learning.Assessments.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Services;

/// <summary>
/// Hears about attempt changes after they are saved. Every method has a default that does nothing.
/// </summary>
public interface IAttemptObserver
{
    /// <summary>
    /// An attempt started.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task StartedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// An attempt was submitted, by the learner or at the deadline.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task SubmittedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// An attempt needs a grader.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task NeedsGradingAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// An attempt was graded, automatically or by a grader.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task GradedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// A score changed after grading: a regrade, an override or a void.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <param name="change">The change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ScoreChangedAsync(AssessmentAttempt attempt, AttemptScoreChange change, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// A person gave a learner an override of a quiz: extra time, extra attempts or another window.
    /// </summary>
    /// <param name="entry">The override.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task OverrideGrantedAsync(AssessmentOverride entry, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

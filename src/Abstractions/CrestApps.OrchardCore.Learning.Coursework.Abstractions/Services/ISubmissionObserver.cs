using CrestApps.OrchardCore.Learning.Coursework.Models;

namespace CrestApps.OrchardCore.Learning.Coursework.Services;

/// <summary>
/// Hears about submissions after the change is committed. An observer that fails is logged and does not undo the
/// change.
/// </summary>
public interface ISubmissionObserver
{
    /// <summary>
    /// Coursework was handed in.
    /// </summary>
    /// <param name="submission">The submission.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task SubmittedAsync(Submission submission, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// A submission was graded, or graded again.
    /// </summary>
    /// <param name="submission">The submission.</param>
    /// <param name="previous">The grade it replaced, or <see langword="null"/> the first time.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task GradedAsync(Submission submission, SubmissionGrade previous, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// A submission was returned to the learner to hand in again.
    /// </summary>
    /// <param name="submission">The submission.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ReturnedAsync(Submission submission, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

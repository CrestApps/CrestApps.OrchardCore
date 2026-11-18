using CrestApps.OrchardCore.Learning.Programs.Models;

namespace CrestApps.OrchardCore.Learning.Programs.Services;

/// <summary>
/// Starts people on learning paths, opens each stage's courses as the one before it is done, and completes the path.
/// </summary>
public interface ILearningPathService
{
    /// <summary>
    /// Starts a person on a path and enrolls them in the courses of its first stage. Starting someone already on the
    /// path returns their progress.
    /// </summary>
    Task<LearningPathStartResult> StartAsync(LearningPathStartRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Works out a person's progress on a path again from their enrollments: opens the next stage, or completes it.
    /// </summary>
    Task<LearningPathProgress> EvaluateAsync(string pathId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Works out again the progress of a person on every path that holds a course.
    /// </summary>
    Task EvaluateCourseAsync(string userId, string courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells whether a person's completed path may support what is issued on it, such as the path's digital credential
    /// or its education credits (which can meet a certification's credits requirement). The path's rule is applied
    /// again counting only the courses whose completion supports issuing
    /// (<see cref="CrestApps.OrchardCore.Learning.Services.CompletionEvidence.SupportsIssuing"/>): a failed course, one
    /// under review, or one resting on a result the browser reported does not count, even though it completes the path
    /// for the learner.
    /// </summary>
    Task<bool> SupportsIssuingAsync(LearningPathProgress progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a person's progress on a path.
    /// </summary>
    Task<LearningPathProgress> FindProgressAsync(string pathId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a person's paths.
    /// </summary>
    Task<IReadOnlyList<LearningPathProgress>> ListForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages the people on a path.
    /// </summary>
    Task<(int Count, IReadOnlyList<LearningPathProgress> Items)> PageAsync(string pathId, LearningPathState? state, int skip, int take, CancellationToken cancellationToken = default);
}

/// <summary>
/// What starts a person on a learning path.
/// </summary>
public sealed class LearningPathStartRequest
{
    /// <summary>
    /// Gets or sets the content item id of the path.
    /// </summary>
    public string PathId { get; set; }

    /// <summary>
    /// Gets or sets the person.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets what starts it.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the record in <see cref="Source"/>.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets when the courses are due.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets who starts it.
    /// </summary>
    public string Actor { get; set; }
}

/// <summary>
/// The result of starting a person on a learning path.
/// </summary>
/// <param name="Progress">The progress, or <see langword="null"/> when it was refused.</param>
/// <param name="Created">Whether the person was started now.</param>
/// <param name="Error">Why it was refused.</param>
public sealed record LearningPathStartResult(LearningPathProgress Progress, bool Created, string Error = null);

/// <summary>
/// Hears that people complete learning paths. Register one with <c>services.AddLearningPathObserver&lt;T&gt;()</c>.
/// </summary>
public interface ILearningPathObserver
{
    /// <summary>
    /// Called after a person completed a path.
    /// </summary>
    Task CompletedAsync(LearningPathProgress progress, CancellationToken cancellationToken = default);
}

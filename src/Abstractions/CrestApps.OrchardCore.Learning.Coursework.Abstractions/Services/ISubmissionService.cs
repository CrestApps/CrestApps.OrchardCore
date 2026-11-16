using CrestApps.OrchardCore.Learning.Coursework.Models;

namespace CrestApps.OrchardCore.Learning.Coursework.Services;

/// <summary>
/// Hands in coursework for a learner, grades it and returns it for resubmission. Every write runs under one lock per
/// enrollment and coursework item on a fresh read, and reports the grade to Learning so it reaches the course grade.
/// </summary>
public interface ISubmissionService
{
    /// <summary>
    /// Hands in coursework for a learner under their own active enrollment in the course of the item. The files are
    /// kept in the protected store before the record is saved, and removed again when anything fails. Past the
    /// coursework's close date only work a grader returned is taken.
    /// </summary>
    /// <param name="request">The learner, the coursework item, the text and the files.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The new submission, or why it was refused.</returns>
    Task<SubmissionResult> SubmitAsync(SubmitCourseworkRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grades the current submission with rubric choices or points and feedback. Late work loses the coursework's late
    /// penalty: the raw score, the days late and the penalty are kept on the submission, and the score after the
    /// penalty is the one reported to the course grade. Grading graded work again needs a reason, keeps the earlier
    /// grade and applies the same days late and penalty to the new raw score.
    /// </summary>
    /// <param name="request">The grade.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The graded submission, or why it was refused.</returns>
    Task<SubmissionResult> GradeAsync(GradeSubmissionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current submission to the learner to hand in again, with feedback. Any grade it had stops counting.
    /// </summary>
    /// <param name="submissionId">The submission.</param>
    /// <param name="feedback">What the learner should change.</param>
    /// <param name="actor">The grader.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The returned submission, or why it was refused.</returns>
    Task<SubmissionResult> ReturnAsync(string submissionId, string feedback, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a submission.
    /// </summary>
    /// <param name="submissionId">The submission.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The submission, or <see langword="null"/>.</returns>
    Task<Submission> FindByIdAsync(string submissionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the submissions of a coursework item under an enrollment, newest attempt first.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="courseworkId">The coursework item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The submissions.</returns>
    Task<IReadOnlyList<Submission>> ListForEnrollmentAsync(string enrollmentId, string courseworkId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a page of the submissions of a coursework item, newest first.
    /// </summary>
    /// <param name="courseworkId">The coursework item.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count and the page.</returns>
    Task<(int Count, IReadOnlyList<Submission> Items)> ListByCourseworkAsync(string courseworkId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a page of the submissions that wait for a grader, oldest first.
    /// </summary>
    /// <param name="courseIds">The courses, or <see langword="null"/> for every course.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count and the page.</returns>
    Task<(int Count, IReadOnlyList<Submission> Items)> ListWaitingAsync(IReadOnlyCollection<string> courseIds, int skip, int take, CancellationToken cancellationToken = default);
}

/// <summary>
/// Coursework a learner hands in.
/// </summary>
public sealed class SubmitCourseworkRequest
{
    /// <summary>
    /// Gets or sets the learner; their own active enrollment in the course of the item is used.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the coursework course item.
    /// </summary>
    public string CourseworkId { get; set; }

    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the files.
    /// </summary>
    public IList<SubmissionUpload> Files { get; set; } = [];
}

/// <summary>
/// A file a learner uploads.
/// </summary>
/// <param name="FileName">The file name.</param>
/// <param name="Length">The size in bytes.</param>
/// <param name="OpenReadStream">Opens the content.</param>
public sealed record SubmissionUpload(string FileName, long Length, Func<Stream> OpenReadStream);

/// <summary>
/// A grade of a submission.
/// </summary>
public sealed class GradeSubmissionRequest
{
    /// <summary>
    /// Gets or sets the submission.
    /// </summary>
    public string SubmissionId { get; set; }

    /// <summary>
    /// Gets or sets the grader.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets the level chosen for each rubric criterion, by criterion id, when the coursework has a rubric.
    /// </summary>
    public IDictionary<string, string> RubricChoices { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the points, when the coursework has no rubric.
    /// </summary>
    public decimal? Points { get; set; }

    /// <summary>
    /// Gets or sets the feedback to the learner.
    /// </summary>
    public string Feedback { get; set; }

    /// <summary>
    /// Gets or sets why graded work is graded again.
    /// </summary>
    public string Reason { get; set; }
}

/// <summary>
/// The outcome of a submission change.
/// </summary>
public sealed class SubmissionResult
{
    /// <summary>
    /// Gets or sets the submission.
    /// </summary>
    public Submission Submission { get; set; }

    /// <summary>
    /// Gets or sets why the change was refused, one of <see cref="SubmissionErrors"/>.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets whether the change was made.
    /// </summary>
    public bool Succeeded => Error is null;

    /// <summary>
    /// Creates a refusal.
    /// </summary>
    /// <param name="error">Why.</param>
    /// <param name="submission">The submission, when there is one.</param>
    /// <returns>The result.</returns>
    public static SubmissionResult Fail(string error, Submission submission = null)
        => new() { Error = error, Submission = submission };
}

/// <summary>
/// Why a submission change was refused.
/// </summary>
public static class SubmissionErrors
{
    /// <summary>The item is not coursework of a course.</summary>
    public const string NotCoursework = "NotCoursework";

    /// <summary>The learner has no active enrollment in the course.</summary>
    public const string NotEnrolled = "NotEnrolled";

    /// <summary>Nothing was handed in.</summary>
    public const string Empty = "Empty";

    /// <summary>The coursework takes text.</summary>
    public const string TextRequired = "TextRequired";

    /// <summary>The coursework takes files.</summary>
    public const string FilesRequired = "FilesRequired";

    /// <summary>The coursework takes no files.</summary>
    public const string FilesNotAllowed = "FilesNotAllowed";

    /// <summary>A file's type is not allowed.</summary>
    public const string ExtensionNotAllowed = "ExtensionNotAllowed";

    /// <summary>A file is too large.</summary>
    public const string FileTooLarge = "FileTooLarge";

    /// <summary>Too many files.</summary>
    public const string TooManyFiles = "TooManyFiles";

    /// <summary>The text is too long.</summary>
    public const string TextTooLong = "TextTooLong";

    /// <summary>The store refused a file, such as an antivirus scanner.</summary>
    public const string FileRefused = "FileRefused";

    /// <summary>Work waits for a grader and the coursework does not take a resubmission.</summary>
    public const string AlreadySubmitted = "AlreadySubmitted";

    /// <summary>The work is graded; it can be handed in again only once a grader returns it.</summary>
    public const string AlreadyGraded = "AlreadyGraded";

    /// <summary>Another change is being made.</summary>
    public const string Busy = "Busy";

    /// <summary>No such submission.</summary>
    public const string NotFound = "NotFound";

    /// <summary>The submission is not the learner's latest, or is with the learner to hand in again.</summary>
    public const string NotGradable = "NotGradable";

    /// <summary>Graded work is graded again without a reason.</summary>
    public const string ReasonRequired = "ReasonRequired";

    /// <summary>The points are out of range, or a rubric criterion has no valid level.</summary>
    public const string InvalidScore = "InvalidScore";

    /// <summary>Nobody grades or returns their own work.</summary>
    public const string OwnSubmission = "OwnSubmission";

    /// <summary>The coursework stopped taking work some days after the due date.</summary>
    public const string Closed = "Closed";
}

using CrestApps.OrchardCore.Learning.Coursework.Models;

namespace CrestApps.OrchardCore.Learning.Coursework.Services;

/// <summary>
/// Hands in coursework for a learner, grades it and returns it for resubmission. Every write runs under one lock per
/// enrollment and coursework item on a fresh read, and reports the grade to Learning so it reaches the course grade.
/// </summary>
public interface ISubmissionService
{
    /// <summary>
    /// Hands in coursework for a learner under their own active enrollment in the course of the item, within the
    /// enrollment's access dates and once the item's release rules open it, as for any other item. The files are
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
    /// grade and applies the same days late and penalty to the new raw score. The grade is recorded on Learning's manual
    /// path (no access window or release check); when Learning still refuses it, such as on a suspended enrollment, the
    /// grading fails with <see cref="SubmissionErrors.EnrollmentNotActive"/>, <see cref="SubmissionErrors.Busy"/> or
    /// <see cref="SubmissionErrors.NotRecorded"/> and the submission is not changed.
    /// </summary>
    /// <param name="request">The grade.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The graded submission, or why it was refused.</returns>
    Task<SubmissionResult> GradeAsync(GradeSubmissionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the record a grader grades to sign off coursework that takes no hand-in
    /// (<see cref="CourseworkSubmissionType.SignOff"/>) for one learner: an empty current submission under the
    /// learner's enrollment, or the current one when there is already one. Opening it reports nothing to the learner's
    /// progress; grading it does, as for handed-in work.
    /// </summary>
    /// <param name="request">The coursework item, the enrollment and the grader.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The record to grade, or why it was refused.</returns>
    Task<SubmissionResult> StartSignOffAsync(SignOffRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that scoring help gave a grader a suggestion for a submission: the next grade that grader saves for it is
    /// AI-assisted. A grade saved again with the same score keeps the mark.
    /// </summary>
    /// <param name="submissionId">The submission.</param>
    /// <param name="grader">The grader.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The submission, or why nothing was recorded.</returns>
    Task<SubmissionResult> RecordScoringHelpAsync(string submissionId, string grader, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current submission to the learner to hand in again, with feedback. Any grade it had stops counting.
    /// </summary>
    /// <param name="submissionId">The submission.</param>
    /// <param name="feedback">What the learner should change.</param>
    /// <param name="actor">The grader.</param>
    /// <param name="canOverrideGrades">Whether the grader may change a grade (Override grades), which returning graded work does; checked on the fresh read under the lock.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The returned submission, or why it was refused.</returns>
    Task<SubmissionResult> ReturnAsync(string submissionId, string feedback, string actor, bool canOverrideGrades = true, CancellationToken cancellationToken = default);

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
/// A sign-off a grader opens for one learner.
/// </summary>
public sealed class SignOffRequest
{
    /// <summary>
    /// Gets or sets the coursework course item.
    /// </summary>
    public string CourseworkId { get; set; }

    /// <summary>
    /// Gets or sets the learner's enrollment in the course of the item.
    /// </summary>
    public string EnrollmentId { get; set; }

    /// <summary>
    /// Gets or sets the grader.
    /// </summary>
    public string Actor { get; set; }
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

    /// <summary>
    /// Gets or sets whether the grader may change a grade (Override grades). It is checked on the fresh read under the
    /// lock, so work another grader graded meanwhile is not graded again without it. <see langword="true"/> by default,
    /// for callers that check no permission, such as a workflow.
    /// </summary>
    public bool CanOverrideGrades { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the grader says they used scoring help. Not trusted and not read: the submission service
    /// decides from the suggestion it recorded (<see cref="ISubmissionService.RecordScoringHelpAsync"/>).
    /// </summary>
    public bool AIAssisted { get; set; }
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

    /// <summary>
    /// The learner's enrollment is not active (suspended, ended or withdrawn), so a grade or a return cannot reach the
    /// learner's progress; nothing was changed.
    /// </summary>
    public const string EnrollmentNotActive = "EnrollmentNotActive";

    /// <summary>Learning refused the result for another reason, such as an item no longer in the course; nothing was changed.</summary>
    public const string NotRecorded = "NotRecorded";

    /// <summary>The learner's access to the course ended.</summary>
    public const string AccessEnded = "AccessEnded";

    /// <summary>The coursework is not open to the learner yet: a release rule, such as an earlier item or a date, keeps it locked.</summary>
    public const string NotAvailableYet = "NotAvailableYet";

    /// <summary>The work is graded, and changing its grade (grading it again or returning it) needs Override grades.</summary>
    public const string OverrideRequired = "OverrideRequired";

    /// <summary>The feedback is longer than <c>CourseworkConstants.MaxFeedbackLength</c>.</summary>
    public const string FeedbackTooLong = "FeedbackTooLong";

    /// <summary>The reason is longer than <c>CourseworkConstants.MaxReasonLength</c>.</summary>
    public const string ReasonTooLong = "ReasonTooLong";

    /// <summary>The coursework takes no hand-in: a grader signs it off.</summary>
    public const string SignOffOnly = "SignOffOnly";

    /// <summary>The coursework takes work from learners, so it is graded from their hand-ins, not signed off.</summary>
    public const string NotSignOff = "NotSignOff";
}

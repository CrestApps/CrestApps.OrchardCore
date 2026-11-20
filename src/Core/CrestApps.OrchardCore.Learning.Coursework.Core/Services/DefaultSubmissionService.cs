using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Coursework.Core.Services;

/// <summary>
/// Hands in, grades and returns coursework. Every write runs under one lock per enrollment and coursework item on a
/// fresh read of the submission, commits before the lock is let go, and tells the observers after the commit. A hand-in
/// saves the submission before it reports to Learning (whose progress write commits the session), so the learner's work
/// is kept even when Learning does not take the result. A grade or a return is a staff write: it reports to Learning on
/// the manual path first and changes the submission only once Learning took it, so a refused grade fails with a reason
/// instead of showing as graded and never reaching the gradebook. A write that reaches the
/// enrollment takes the enrollment lock inside this one; enrollment writes never take a coursework lock, so the order
/// cannot deadlock.
/// </summary>
internal sealed class DefaultSubmissionService : ISubmissionService
{
    /// <summary>
    /// The longest text a submission keeps.
    /// </summary>
    internal const int MaxTextLength = 100_000;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(60);

    private readonly SubmissionStore _store;
    private readonly ICourseworkContentSource _contentSource;
    private readonly ICourseStructureService _courseStructure;
    private readonly CourseReleaseEvaluator _release;
    private readonly IEnrollmentStore _enrollments;
    private readonly ILearnerProgressService _progress;
    private readonly IProtectedFileStore<CourseworkFiles> _files;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IEnumerable<ISubmissionObserver> _observers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DefaultSubmissionService(
        SubmissionStore store,
        ICourseworkContentSource contentSource,
        ICourseStructureService courseStructure,
        CourseReleaseEvaluator release,
        IEnrollmentStore enrollments,
        ILearnerProgressService progress,
        IProtectedFileStore<CourseworkFiles> files,
        IDistributedLock distributedLock,
        ISession session,
        IEnumerable<ISubmissionObserver> observers,
        IClock clock,
        ILogger<DefaultSubmissionService> logger)
    {
        _store = store;
        _contentSource = contentSource;
        _courseStructure = courseStructure;
        _release = release;
        _enrollments = enrollments;
        _progress = progress;
        _files = files;
        _distributedLock = distributedLock;
        _session = session;
        _observers = observers;
        _clock = clock;
        _logger = logger;
    }

    public async Task<SubmissionResult> SubmitAsync(SubmitCourseworkRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.UserId))
        {
            return SubmissionResult.Fail(SubmissionErrors.NotEnrolled);
        }

        var coursework = await FindCourseworkAsync(request.CourseworkId);

        if (coursework is null)
        {
            return SubmissionResult.Fail(SubmissionErrors.NotCoursework);
        }

        // Only the learner's own active enrollment in the item's course; the user id is checked again so another
        // learner's enrollment is never used.
        var enrollment = (await _enrollments.ListByCourseForUserAsync(request.UserId, coursework.CourseId, cancellationToken))
            .FirstOrDefault(candidate => string.Equals(candidate.UserId, request.UserId, StringComparison.Ordinal) &&
                string.Equals(candidate.CourseId, coursework.CourseId, StringComparison.Ordinal) &&
                candidate.Status == EnrollmentStatus.Active);

        if (enrollment is null)
        {
            return SubmissionResult.Fail(SubmissionErrors.NotEnrolled);
        }

        // As for any other item of the course: within the learner's access dates, once the release rules open it. The
        // service has no signed-in user to ask the course access service with, so the enrollment rules are checked here.
        if (!EnrollmentAccess.HasAccess(enrollment, _clock.UtcNow))
        {
            return SubmissionResult.Fail(SubmissionErrors.AccessEnded);
        }

        if (!(await _release.EvaluateAsync(coursework.Entry, enrollment, await _progress.GetAsync(enrollment.ItemId, cancellationToken))).IsReleased)
        {
            return SubmissionResult.Fail(SubmissionErrors.NotAvailableYet);
        }

        var text =string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim();
        var uploads = (request.Files ?? []).Where(upload => upload is not null).ToList();
        var refused = Validate(coursework.Part, ref text, uploads);

        if (refused is not null)
        {
            return SubmissionResult.Fail(refused);
        }

        Submission submission = null;

        var result = await RunLockedAsync(enrollment.ItemId, coursework.Item.ContentItemId, async () =>
        {
            var earlier = await _store.ListForEnrollmentAsync(enrollment.ItemId, coursework.Item.ContentItemId, cancellationToken);
            var current = earlier.FirstOrDefault(candidate => candidate.Current);

            if (current is not null)
            {
                current = await _store.FindFreshAsync(current.ItemId, cancellationToken);
            }

            if (current?.Status == SubmissionStatus.Graded)
            {
                return SubmissionResult.Fail(SubmissionErrors.AlreadyGraded, current);
            }

            if (current?.Status == SubmissionStatus.Submitted && !coursework.Part.AllowResubmission)
            {
                return SubmissionResult.Fail(SubmissionErrors.AlreadySubmitted, current);
            }

            // Past the close date only work a grader returned is taken again.
            if (current?.Status != SubmissionStatus.Returned && CourseworkLatePolicy.IsClosed(coursework.Part, _clock.UtcNow))
            {
                return SubmissionResult.Fail(SubmissionErrors.Closed, current);
            }

            // The files are kept before the record is saved, and removed again when anything after fails.
            var stored = new List<SubmissionFile>();

            try
            {
                var maxBytes = coursework.Part.MaxFileSizeMb * 1024L * 1024L;

                foreach (var upload in uploads)
                {
                    await using var content = upload.OpenReadStream();
                    var saved = await _files.SaveAsync(upload.FileName, content, cancellationToken);

                    stored.Add(new SubmissionFile { FileId = saved.FileId, FileName = saved.FileName, Size = saved.Size });

                    if (saved.Size > maxBytes)
                    {
                        await DeleteFilesAsync(stored, cancellationToken);

                        return SubmissionResult.Fail(SubmissionErrors.FileTooLarge);
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "A file handed in for coursework {CourseworkId} was refused by the protected store.", coursework.Item.ContentItemId);
                await DeleteFilesAsync(stored, cancellationToken);

                return SubmissionResult.Fail(SubmissionErrors.FileRefused);
            }

            try
            {
                var now = _clock.UtcNow;
                var dueUtc = coursework.Part.DueUtc is { } published ? DateTime.SpecifyKind(published, DateTimeKind.Utc) : (DateTime?)null;

                if (current is not null)
                {
                    current.Current = false;
                    await _store.UpdateAsync(current, cancellationToken);
                }

                submission = new Submission
                {
                    ItemId = IdGenerator.GenerateId(),
                    EnrollmentId = enrollment.ItemId,
                    UserId = enrollment.UserId,
                    CourseId = coursework.CourseId,
                    CourseworkId = coursework.Item.ContentItemId,
                    CourseworkTitle = coursework.Item.DisplayText,
                    Attempt = (earlier.Count == 0 ? 0 : earlier.Max(candidate => candidate.Attempt)) + 1,
                    Current = true,
                    Text = text,
                    Files = stored,
                    SubmittedUtc = now,

                    // The due date is kept with the work, and the late flag and the penalty are worked out from it, so
                    // moving the due date afterwards changes neither. Work a grader returned and the learner hands in
                    // again after the due date is late by its new hand-in time and takes the full penalty, including the
                    // days it sat with the grader: that is the policy, not an oversight (plan section 19).
                    DueUtc = dueUtc,
                    Late = dueUtc is { } due && now > due,
                    Status = SubmissionStatus.Submitted,
                };

                await _store.CreateAsync(submission, cancellationToken);
                await ReportAsync(enrollment.ItemId, new CourseItemResult
                {
                    ItemId = submission.CourseworkId,
                    Submitted = true,
                    Attempts = submission.Attempt,
                }, cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Coursework {CourseworkId} could not be handed in; its files were removed.", coursework.Item.ContentItemId);
                await DeleteFilesAsync(stored, cancellationToken);

                throw;
            }

            return new SubmissionResult { Submission = submission };
        }, cancellationToken);

        if (result.Succeeded && submission is not null)
        {
            await NotifyAsync(observer => observer.SubmittedAsync(submission, cancellationToken));
        }

        return result;
    }

    public async Task<SubmissionResult> StartSignOffAsync(SignOffRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var coursework = await FindCourseworkAsync(request.CourseworkId);

        if (coursework is null)
        {
            return SubmissionResult.Fail(SubmissionErrors.NotCoursework);
        }

        if (coursework.Part.SubmissionType != CourseworkSubmissionType.SignOff)
        {
            return SubmissionResult.Fail(SubmissionErrors.NotSignOff);
        }

        var enrollment = string.IsNullOrEmpty(request.EnrollmentId) ? null : await _enrollments.FindByIdAsync(request.EnrollmentId, cancellationToken);

        // Only an enrollment in the item's own course, open to grading.
        if (enrollment is null ||
            !string.Equals(enrollment.CourseId, coursework.CourseId, StringComparison.Ordinal) ||
            enrollment.Status is not (EnrollmentStatus.Active or EnrollmentStatus.Completed))
        {
            return SubmissionResult.Fail(SubmissionErrors.NotEnrolled);
        }

        if (!string.IsNullOrEmpty(request.Actor) && string.Equals(enrollment.UserId, request.Actor, StringComparison.Ordinal))
        {
            return SubmissionResult.Fail(SubmissionErrors.OwnSubmission);
        }

        return await RunLockedAsync(enrollment.ItemId, coursework.Item.ContentItemId, async () =>
        {
            var earlier = await _store.ListForEnrollmentAsync(enrollment.ItemId, coursework.Item.ContentItemId, cancellationToken);
            var current = earlier.FirstOrDefault(candidate => candidate.Current);

            if (current is not null)
            {
                return new SubmissionResult { Submission = await _store.FindFreshAsync(current.ItemId, cancellationToken) ?? current };
            }

            // Nothing is reported to the learner's progress: the item counts once it is graded.
            var submission = new Submission
            {
                ItemId = IdGenerator.GenerateId(),
                EnrollmentId = enrollment.ItemId,
                UserId = enrollment.UserId,
                CourseId = coursework.CourseId,
                CourseworkId = coursework.Item.ContentItemId,
                CourseworkTitle = coursework.Item.DisplayText,
                Attempt = (earlier.Count == 0 ? 0 : earlier.Max(candidate => candidate.Attempt)) + 1,
                Current = true,
                SignOff = true,
                SubmittedUtc = _clock.UtcNow,
                Status = SubmissionStatus.Submitted,
            };

            await _store.CreateAsync(submission, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return new SubmissionResult { Submission = submission };
        }, cancellationToken);
    }

    public async Task<SubmissionResult> GradeAsync(GradeSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = string.IsNullOrEmpty(request.SubmissionId) ? null : await _store.FindByIdAsync(request.SubmissionId, cancellationToken);

        if (existing is null)
        {
            return SubmissionResult.Fail(SubmissionErrors.NotFound);
        }

        if (IsOwn(existing, request.Actor))
        {
            return SubmissionResult.Fail(SubmissionErrors.OwnSubmission, existing);
        }

        // The page limits these too, but a post does not have to come from the page.
        if (request.Feedback?.Trim().Length > CourseworkConstants.MaxFeedbackLength)
        {
            return SubmissionResult.Fail(SubmissionErrors.FeedbackTooLong, existing);
        }

        if (request.Reason?.Trim().Length > CourseworkConstants.MaxReasonLength)
        {
            return SubmissionResult.Fail(SubmissionErrors.ReasonTooLong, existing);
        }

        Submission graded = null;
        SubmissionGrade previous = null;

        var result = await RunLockedAsync(existing.EnrollmentId, existing.CourseworkId, async () =>
        {
            var submission = await _store.FindFreshAsync(existing.ItemId, cancellationToken);

            if (submission is null)
            {
                return SubmissionResult.Fail(SubmissionErrors.NotFound);
            }

            if (!submission.Current || submission.Status == SubmissionStatus.Returned)
            {
                return SubmissionResult.Fail(SubmissionErrors.NotGradable, submission);
            }

            var regrade = submission.Status == SubmissionStatus.Graded;

            // Decided on this fresh read: the caller read the work before the lock, and another grader may have graded it since.
            if (regrade && !request.CanOverrideGrades)
            {
                return SubmissionResult.Fail(SubmissionErrors.OverrideRequired, submission);
            }

            if (regrade && string.IsNullOrWhiteSpace(request.Reason))
            {
                return SubmissionResult.Fail(SubmissionErrors.ReasonRequired, submission);
            }

            var part = (await _contentSource.GetPublishedAsync(submission.CourseworkId)).FindPart<CourseworkPart>();
            var score = await ScoreAsync(part, request);

            if (score is null)
            {
                return SubmissionResult.Fail(SubmissionErrors.InvalidScore, submission);
            }

            // The days late and the penalty are set when the work is first graded; a regrade applies them again.
            var (daysLate, penalty) = CourseworkLatePolicy.Resolve(submission, part);
            var finalScore = CourseworkLatePolicy.Apply(score.Percent, penalty);

            // A grade is reported before the submission changes: Learning commits its write whether or not it takes the
            // result, so a grade it refuses must leave nothing behind to commit, and the grader is told.
            var refused = await ReportStaffAsync(submission.EnrollmentId, new CourseItemResult
            {
                ItemId = submission.CourseworkId,
                Score = finalScore,
                Submitted = true,
                Graded = true,
                Attempts = submission.Attempt,
            }, request.Actor, cancellationToken);

            if (refused is not null)
            {
                return SubmissionResult.Fail(refused, submission);
            }

            submission = await ReadAgainAsync(submission, cancellationToken);

            if (submission is null)
            {
                return SubmissionResult.Fail(SubmissionErrors.NotFound);
            }

            var now = _clock.UtcNow;

            // AI-assisted when scoring help gave this grader a suggestion for the work, or when an assisted grade is
            // saved again with the same score; never because the form says so (review round 10, item 1).
            var aiAssisted = string.Equals(submission.ScoringHelpGivenTo, request.Actor, StringComparison.Ordinal)
                || (regrade && submission.AIAssisted && submission.RawScore == score.Percent && submission.Points == score.Points);

            if (regrade)
            {
                previous = KeepGrade(submission, request.Actor, now, request.Reason.Trim());
            }

            submission.Status = SubmissionStatus.Graded;
            submission.RawScore = score.Percent;
            submission.DaysLate = daysLate;
            submission.LatePenaltyPercent = penalty;
            submission.Score = finalScore;
            submission.Points = score.Points;
            submission.MaxPoints = score.MaxPoints;
            submission.RubricId = score.RubricId;
            submission.RubricChoices = score.Choices;
            submission.Feedback = string.IsNullOrWhiteSpace(request.Feedback) ? submission.Feedback : request.Feedback.Trim();
            submission.GraderId = request.Actor;
            submission.GradedUtc = now;
            submission.AIAssisted = aiAssisted;
            submission.ScoringHelpGivenTo = null;

            await _store.UpdateAsync(submission, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            graded = submission;

            return new SubmissionResult { Submission = submission };
        }, cancellationToken);

        if (result.Succeeded && graded is not null)
        {
            await NotifyAsync(observer => observer.GradedAsync(graded, previous, cancellationToken));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<SubmissionResult> RecordScoringHelpAsync(string submissionId, string grader, CancellationToken cancellationToken = default)
    {
        var existing = string.IsNullOrEmpty(submissionId) ? null : await _store.FindByIdAsync(submissionId, cancellationToken);

        if (existing is null || string.IsNullOrEmpty(grader))
        {
            return SubmissionResult.Fail(SubmissionErrors.NotFound);
        }

        return await RunLockedAsync(existing.EnrollmentId, existing.CourseworkId, async () =>
        {
            var submission = await _store.FindFreshAsync(existing.ItemId, cancellationToken);

            if (submission is null)
            {
                return SubmissionResult.Fail(SubmissionErrors.NotFound);
            }

            submission.ScoringHelpGivenTo = grader;

            await _store.UpdateAsync(submission, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return new SubmissionResult { Submission = submission };
        }, cancellationToken);
    }

    public async Task<SubmissionResult> ReturnAsync(string submissionId, string feedback, string actor, bool canOverrideGrades = true, CancellationToken cancellationToken = default)
    {
        var existing = string.IsNullOrEmpty(submissionId) ? null : await _store.FindByIdAsync(submissionId, cancellationToken);

        if (existing is null)
        {
            return SubmissionResult.Fail(SubmissionErrors.NotFound);
        }

        if (IsOwn(existing, actor))
        {
            return SubmissionResult.Fail(SubmissionErrors.OwnSubmission, existing);
        }

        if (feedback?.Trim().Length > CourseworkConstants.MaxFeedbackLength)
        {
            return SubmissionResult.Fail(SubmissionErrors.FeedbackTooLong, existing);
        }

        Submission returned = null;

        var result = await RunLockedAsync(existing.EnrollmentId, existing.CourseworkId, async () =>
        {
            var submission = await _store.FindFreshAsync(existing.ItemId, cancellationToken);

            if (submission is null)
            {
                return SubmissionResult.Fail(SubmissionErrors.NotFound);
            }

            if (!submission.Current || submission.Status == SubmissionStatus.Returned)
            {
                return SubmissionResult.Fail(SubmissionErrors.NotGradable, submission);
            }

            // Returning graded work takes its grade away, which changes a grade: decided on this fresh read.
            if (submission.Status == SubmissionStatus.Graded && !canOverrideGrades)
            {
                return SubmissionResult.Fail(SubmissionErrors.OverrideRequired, submission);
            }

            // As for a grade, Learning hears of the return first, so a return it refuses changes nothing.
            var refused = await ReportStaffAsync(submission.EnrollmentId, new CourseItemResult
            {
                ItemId = submission.CourseworkId,
                ResetScore = true,
                Attempts = submission.Attempt,
            }, actor, cancellationToken);

            if (refused is not null)
            {
                return SubmissionResult.Fail(refused, submission);
            }

            submission = await ReadAgainAsync(submission, cancellationToken);

            if (submission is null)
            {
                return SubmissionResult.Fail(SubmissionErrors.NotFound);
            }

            var now = _clock.UtcNow;

            // A grade the work had stops counting; it is kept in the history.
            if (submission.Status == SubmissionStatus.Graded)
            {
                KeepGrade(submission, actor, now, "Returned for resubmission.");
                submission.Score = null;
                submission.RawScore = null;
                submission.LatePenaltyPercent = null;
                submission.Points = null;
                submission.MaxPoints = null;
                submission.RubricChoices = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            submission.Status = SubmissionStatus.Returned;
            submission.Feedback = string.IsNullOrWhiteSpace(feedback) ? submission.Feedback : feedback.Trim();
            submission.ReturnedBy = actor;
            submission.ReturnedUtc = now;

            await _store.UpdateAsync(submission, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            returned = submission;

            return new SubmissionResult { Submission = submission };
        }, cancellationToken);

        if (result.Succeeded && returned is not null)
        {
            await NotifyAsync(observer => observer.ReturnedAsync(returned, cancellationToken));
        }

        return result;
    }

    public async Task<Submission> FindByIdAsync(string submissionId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(submissionId) ? null : await _store.FindByIdAsync(submissionId, cancellationToken);

    public Task<IReadOnlyList<Submission>> ListForEnrollmentAsync(string enrollmentId, string courseworkId, CancellationToken cancellationToken = default)
        => _store.ListForEnrollmentAsync(enrollmentId, courseworkId, cancellationToken);

    public Task<(int Count, IReadOnlyList<Submission> Items)> ListByCourseworkAsync(string courseworkId, int skip, int take, CancellationToken cancellationToken = default)
        => _store.PageByCourseworkAsync(courseworkId, skip, take, cancellationToken);

    public Task<(int Count, IReadOnlyList<Submission> Items)> ListWaitingAsync(IReadOnlyCollection<string> courseIds, int skip, int take, CancellationToken cancellationToken = default)
        => _store.PageWaitingAsync(courseIds, skip, take, cancellationToken);

    /// <summary>
    /// Gets the lock key of the submissions of a learner's enrollment on a coursework item.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="courseworkId">The coursework item.</param>
    /// <returns>The key.</returns>
    internal static string LockKey(string enrollmentId, string courseworkId)
        => $"Learning:CourseworkWrite:{enrollmentId}:{courseworkId}";

    private static string Validate(CourseworkPart part, ref string text, List<SubmissionUpload> uploads)
    {
        switch (part.SubmissionType)
        {
            case CourseworkSubmissionType.SignOff:
                return SubmissionErrors.SignOffOnly;

            case CourseworkSubmissionType.Text:
                if (uploads.Count > 0)
                {
                    return SubmissionErrors.FilesNotAllowed;
                }

                if (text is null)
                {
                    return SubmissionErrors.TextRequired;
                }

                break;

            case CourseworkSubmissionType.Files:
                if (uploads.Count == 0)
                {
                    return SubmissionErrors.FilesRequired;
                }

                // A files-only coursework keeps no text.
                text = null;
                break;

            default:
                if (text is null && uploads.Count == 0)
                {
                    return SubmissionErrors.Empty;
                }

                break;
        }

        if (text?.Length > MaxTextLength)
        {
            return SubmissionErrors.TextTooLong;
        }

        if (uploads.Count > Math.Max(part.MaxFiles, 1))
        {
            return SubmissionErrors.TooManyFiles;
        }

        var allowed = (part.AllowedExtensions ?? []).Where(extension => !string.IsNullOrWhiteSpace(extension)).ToArray();
        var maxBytes = part.MaxFileSizeMb * 1024L * 1024L;

        foreach (var upload in uploads)
        {
            var extension = Path.GetExtension(upload.FileName ?? string.Empty);

            if (allowed.Length > 0 && !allowed.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return SubmissionErrors.ExtensionNotAllowed;
            }

            if (upload.Length > maxBytes)
            {
                return SubmissionErrors.FileTooLarge;
            }
        }

        return null;
    }

    private async Task<CourseworkTarget> FindCourseworkAsync(string courseworkId)
    {
        var item = await _contentSource.GetPublishedAsync(courseworkId);

        if (item is null || !item.Has<CourseworkPart>())
        {
            return null;
        }

        var courseId = _courseStructure.GetCourseId(item);
        var course = string.IsNullOrEmpty(courseId) ? null : await _courseStructure.GetCourseAsync(courseId);

        if (course is null)
        {
            return null;
        }

        var entry = (await _courseStructure.GetOutlineAsync(course)).Find(item.ContentItemId);

        if (entry?.Part is null || !string.Equals(entry.Part.Kind, CourseworkConstants.CourseworkKind, StringComparison.Ordinal))
        {
            return null;
        }

        var outlineItem = entry.Item ?? item;

        return new CourseworkTarget(outlineItem, courseId, outlineItem.FindPart<CourseworkPart>() ?? item.FindPart<CourseworkPart>(), entry);
    }

    private async Task<GradeScore> ScoreAsync(CourseworkPart part, GradeSubmissionRequest request)
    {
        var rubricId = part?.RubricId;
        var rubric = string.IsNullOrEmpty(rubricId) ? null : (await _contentSource.GetPublishedAsync(rubricId)).FindPart<RubricPart>();

        if (rubric is not null)
        {
            var choices = request.RubricChoices ?? new Dictionary<string, string>(StringComparer.Ordinal);
            var rubricScore = RubricScoring.Score(rubric, choices);

            if (!rubricScore.Complete || rubricScore.MaxPoints <= 0)
            {
                return null;
            }

            // Only the choices of the rubric's own criteria are kept. The editor refuses two criteria with one id, but a
            // rubric saved another way (a recipe, the content API) may repeat one: it is kept once, not thrown on.
            var kept = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var criterion in rubric.Criteria.Where(criterion => criterion.Id is not null && choices.ContainsKey(criterion.Id)))
            {
                kept.TryAdd(criterion.Id, choices[criterion.Id]);
            }

            return new GradeScore(rubricScore.Points, rubricScore.MaxPoints, rubricScore.Percent, rubricId, kept);
        }

        // Without a rubric (or when the rubric is gone) the work is graded out of the coursework's points.
        var max = part?.MaxPoints ?? 100;

        if (request.Points is not { } points || points < 0 || max <= 0 || points > max)
        {
            return null;
        }

        return new GradeScore(points, max, Math.Round(points * 100 / max, 2, MidpointRounding.AwayFromZero), null, new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private static SubmissionGrade KeepGrade(Submission submission, string actor, DateTime now, string reason)
    {
        var grade = new SubmissionGrade
        {
            Score = submission.Score,
            RawScore = submission.RawScore,
            LatePenaltyPercent = submission.LatePenaltyPercent,
            Points = submission.Points,
            MaxPoints = submission.MaxPoints,
            RubricChoices = new Dictionary<string, string>(submission.RubricChoices ?? [], StringComparer.Ordinal),
            Feedback = submission.Feedback,
            GraderId = submission.GraderId,
            GradedUtc = submission.GradedUtc,
            AIAssisted = submission.AIAssisted,
            ReplacedBy = actor,
            ReplacedUtc = now,
            Reason = reason,
        };

        submission.History.Add(grade);

        return grade;
    }

    private async Task ReportAsync(string enrollmentId, CourseItemResult result, CancellationToken cancellationToken)
    {
        var enrollment = string.IsNullOrEmpty(enrollmentId) ? null : await _enrollments.FindByIdAsync(enrollmentId, cancellationToken);

        if (enrollment is null)
        {
            return;
        }

        var update = await _progress.RecordWithRetryAsync(enrollment, result, cancellationToken);

        if (update?.RefusedReason is not null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("The coursework result of item {ItemId} was not recorded on enrollment {EnrollmentId}: {Reason}.", result.ItemId, enrollmentId, update.RefusedReason);
        }
    }

    // Learning commits the session when it records a result, and a commit makes the session forget the documents read
    // before it: saving that copy would add a second record with the same id. The submission is read again under the
    // coursework lock, which keeps everyone else from changing it in between.
    private Task<Submission> ReadAgainAsync(Submission submission, CancellationToken cancellationToken)
        => _store.FindFreshAsync(submission.ItemId, cancellationToken);

    // A grade or a return is a staff write, recorded on Learning's manual path. Returns the error to show when Learning
    // refuses it, or null when it was recorded (or the enrollment is gone, so there is nothing to report to).
    private async Task<string> ReportStaffAsync(string enrollmentId, CourseItemResult result, string actor, CancellationToken cancellationToken)
    {
        var enrollment = string.IsNullOrEmpty(enrollmentId) ? null : await _enrollments.FindByIdAsync(enrollmentId, cancellationToken);

        if (enrollment is null)
        {
            return null;
        }

        var refused = (await _progress.RecordStaffResultAsync(enrollment, result, actor, cancellationToken))?.RefusedReason;

        if (refused is null)
        {
            return null;
        }

        _logger.LogWarning("The coursework result of item {ItemId} was refused on enrollment {EnrollmentId}: {Reason}. Nothing was changed.", result.ItemId, enrollmentId, refused);

        return refused switch
        {
            LearningRefusals.Busy => SubmissionErrors.Busy,
            _ when StaffResultRecorder.IsEnrollmentNotActive(refused) => SubmissionErrors.EnrollmentNotActive,
            _ => SubmissionErrors.NotRecorded,
        };
    }

    private async Task<SubmissionResult> RunLockedAsync(string enrollmentId, string courseworkId, Func<Task<SubmissionResult>> work, CancellationToken cancellationToken)
    {
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey(enrollmentId, courseworkId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return SubmissionResult.Fail(SubmissionErrors.Busy);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await work();
        }
        finally
        {
            await locker.DisposeAsync();
        }
    }

    private async Task DeleteFilesAsync(IEnumerable<SubmissionFile> files, CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            try
            {
                await _files.DeleteAsync(file.FileId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "The submitted file {FileId} could not be removed.", file.FileId);
            }
        }
    }

    private async Task NotifyAsync(Func<ISubmissionObserver, Task> notify)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await notify(observer);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "A submission observer {Observer} failed.", observer.GetType().Name);
            }
        }
    }

    private static bool IsOwn(Submission submission, string actor)
        => !string.IsNullOrEmpty(actor) && string.Equals(submission.UserId, actor, StringComparison.Ordinal);

    private sealed record CourseworkTarget(ContentItem Item, string CourseId, CourseworkPart Part, CourseOutlineEntry Entry);

    private sealed record GradeScore(decimal Points, decimal MaxPoints, decimal Percent, string RubricId, Dictionary<string, string> Choices);
}

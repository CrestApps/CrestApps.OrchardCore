using CrestApps.Core;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Records a learner's work on course items and completes items and courses on the server. Progress is written to its
/// own document; the enrollment is written only when its summary changes (first start, an item completed, the grade,
/// completion) and at most once an hour for the last activity time.
/// </summary>
public sealed class DefaultLearnerProgressService : ILearnerProgressService
{
    /// <summary>
    /// The most seconds one call can add, so a client cannot claim more time than a heartbeat covers.
    /// </summary>
    public const int MaxSecondsPerCall = 60;

    /// <summary>
    /// The share of a video, in percent, a learner may claim beyond what the time since opening it allows, for seeking
    /// and rounding.
    /// </summary>
    public const int ViewedSlackPercent = 5;

    private static readonly TimeSpan _activityWriteInterval = TimeSpan.FromHours(1);

    private readonly EnrollmentStore _enrollmentStore;
    private readonly EnrollmentProgressStore _progressStore;
    private readonly ICourseStructureService _courseStructure;
    private readonly CourseReleaseEvaluator _releaseEvaluator;
    private readonly CourseCompletionEvaluator _completionEvaluator;
    private readonly IEnumerable<IEnrollmentObserver> _observers;
    private readonly EnrollmentWriteGate _writeGate;
    private readonly IEnumerable<ICourseItemHandler> _itemHandlers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultLearnerProgressService"/> class.
    /// </summary>
    /// <param name="enrollmentStore">The enrollment store.</param>
    /// <param name="progressStore">The progress store.</param>
    /// <param name="courseStructure">The course structure reader.</param>
    /// <param name="releaseEvaluator">The release rule evaluator.</param>
    /// <param name="completionEvaluator">The completion evaluator.</param>
    /// <param name="observers">The enrollment observers.</param>
    /// <param name="itemHandlers">The course item handlers.</param>
    /// <param name="writeGate">The per-enrollment write lock.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public DefaultLearnerProgressService(
        EnrollmentStore enrollmentStore,
        EnrollmentProgressStore progressStore,
        ICourseStructureService courseStructure,
        CourseReleaseEvaluator releaseEvaluator,
        CourseCompletionEvaluator completionEvaluator,
        IEnumerable<IEnrollmentObserver> observers,
        IEnumerable<ICourseItemHandler> itemHandlers,
        EnrollmentWriteGate writeGate,
        IClock clock,
        ILogger<DefaultLearnerProgressService> logger)
    {
        _enrollmentStore = enrollmentStore;
        _progressStore = progressStore;
        _courseStructure = courseStructure;
        _releaseEvaluator = releaseEvaluator;
        _completionEvaluator = completionEvaluator;
        _observers = observers;
        _itemHandlers = itemHandlers;
        _writeGate = writeGate;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EnrollmentProgress> GetAsync(string enrollmentId, CancellationToken cancellationToken = default)
    {
        return await _progressStore.FindByEnrollmentAsync(enrollmentId, cancellationToken) ?? new EnrollmentProgress { EnrollmentId = enrollmentId };
    }

    /// <inheritdoc />
    public Task<ProgressUpdate> RecordAccessAsync(Enrollment enrollment, string itemId, int secondsSpent, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(enrollment, itemId, context =>
        {
            // Time counts by the clock: a call adds no more than the wall time since the last access to any item of the
            // enrollment, so a burst of calls or two tabs at once never add more time than passed.
            var elapsed = context.LastAccessUtc is { } last ? Math.Max(0, (int)(context.UtcNow - last).TotalSeconds) : 0;

            context.Item.SecondsSpent += Math.Min(Math.Clamp(secondsSpent, 0, MaxSecondsPerCall), elapsed);

            return null;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ProgressUpdate> RecordViewedAsync(Enrollment enrollment, string itemId, int percentViewed, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(enrollment, itemId, context =>
        {
            var claimed = Math.Clamp(percentViewed, 0, 100);
            var duration = context.Entry.Item.Get<VideoLessonPart>(nameof(VideoLessonPart))?.DurationSeconds;

            if (duration is > 0)
            {
                // With the length known, no more can have been watched than the time since the video was opened allows.
                var watched = context.Item.FirstAccessUtc is { } first ? Math.Max(0, (context.UtcNow - first).TotalSeconds) : 0;
                var possible = (int)Math.Min(100, Math.Floor(watched * 100 / duration.Value) + ViewedSlackPercent);

                claimed = Math.Min(claimed, possible);
            }
            else if (claimed > 0)
            {
                // Without the length the share is only the browser's claim, and reports show it as such.
                context.Item.SelfReported = true;
            }

            context.Item.PercentViewed = Math.Max(context.Item.PercentViewed, claimed);

            return null;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ProgressUpdate> MarkDoneAsync(Enrollment enrollment, string itemId, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(enrollment, itemId, context =>
        {
            if (!string.Equals(context.Entry.Part?.CompletionMode, CompletionModes.MarkedDone, StringComparison.Ordinal))
            {
                return LearningRefusals.NotMarkedDone;
            }

            context.Complete = true;

            return null;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ProgressUpdate> AcknowledgeAsync(Enrollment enrollment, string itemId, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(enrollment, itemId, context =>
        {
            if (!string.Equals(context.Entry.Part?.CompletionMode, CompletionModes.Acknowledged, StringComparison.Ordinal))
            {
                return LearningRefusals.NoStatement;
            }

            if (context.Item.AttestedUtc is null)
            {
                context.Item.AttestedStatement = context.Entry.Item.Get<AcknowledgementPart>(nameof(AcknowledgementPart))?.Statement;
                context.Item.AttestedVersionId = context.Entry.Item.ContentItemVersionId;
                context.Item.AttestedUtc = context.UtcNow;
            }

            return null;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ProgressUpdate> RecordResultAsync(Enrollment enrollment, CourseItemResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        return UpdateAsync(enrollment, result.ItemId, context =>
        {
            var item = context.Item;

            if (result.ResetScore)
            {
                item.Score = null;
                item.Passed = null;

                if (item.State is CourseItemState.Completed or CourseItemState.Failed && string.IsNullOrEmpty(item.CompletedBy))
                {
                    item.State = CourseItemState.InProgress;
                    item.CompletedUtc = null;
                }
            }

            if (result.Score is not null)
            {
                item.Score = Math.Clamp(result.Score.Value, 0, 100);
            }

            item.Attempts = Math.Max(item.Attempts, result.Attempts);
            item.SelfReported = result.SelfReported;
            item.ResultSelfReported = result.SelfReported;

            var passMark = context.Entry.Part?.PassMark;

            item.Passed = result.Passed ?? (passMark is not null && item.Score is not null ? item.Score >= passMark : item.Passed);
            context.Result = result;

            return null;
        }, cancellationToken, manual: !string.IsNullOrEmpty(result.RecordedBy), whenCompleted: completed => AfterCompletionAsync(completed, result, cancellationToken));
    }

    // After completion the enrollment is a record, with two exceptions. A first score of a gradable item, such as work
    // graded after it was handed in, is recorded and the course grade and outcome recomputed, because the completion
    // counted that item as nothing; the enrollment is flagged for review when the outcome changes. Otherwise a result only
    // matters when it undoes what the completion rests on: the item's score was voided, fell, or is no longer a pass. Then
    // the enrollment stays Completed and is flagged for review, once while open.
    private async Task AfterCompletionAsync(CompletedContext context, CourseItemResult result, CancellationToken cancellationToken)
    {
        var enrollment = context.Enrollment;
        var progress = context.Progress;
        var notifications = context.Notifications;
        var entry = context.Outline.Find(result.ItemId);
        var existing = progress.Find(result.ItemId);

        if (entry?.Part?.Gradable == true && existing?.Score is null && result.Score is not null && !result.ResetScore)
        {
            await RecordFirstScoreAfterCompletionAsync(context, entry, result, cancellationToken);

            return;
        }

        await FlagForReviewAsync(enrollment, progress, result, notifications, cancellationToken);
    }

    private async Task RecordFirstScoreAfterCompletionAsync(CompletedContext context, CourseOutlineEntry entry, CourseItemResult result, CancellationToken cancellationToken)
    {
        var enrollment = context.Enrollment;
        var progress = context.Progress;
        var now = _clock.UtcNow;
        var item = progress.Find(result.ItemId);

        if (item is null)
        {
            item = new CourseItemProgress { ContentItemId = result.ItemId, State = CourseItemState.NotStarted };
            progress.Items.Add(item);
        }

        item.Score = Math.Clamp(result.Score.Value, 0, 100);
        item.Passed = result.Passed ?? (entry.Part.PassMark is { } passMark ? item.Score >= passMark : item.Passed);
        item.SelfReported = result.SelfReported;
        item.ResultSelfReported = result.SelfReported;
        progress.ModifiedUtc = now;

        if (context.IsNewProgress)
        {
            await _progressStore.CreateAsync(progress, cancellationToken);
        }
        else
        {
            await _progressStore.SaveAsync(progress, cancellationToken);
        }

        var grade = CourseGradeCalculator.Compute(context.Outline, progress);
        var passed = CourseGradeCalculator.IsPassed(grade, context.Course.Get<CoursePart>(nameof(CoursePart))?.PassMark);
        var outcomeChanged = passed != enrollment.Passed;

        enrollment.Grade = grade;
        enrollment.Passed = passed;
        enrollment.ModifiedUtc = now;

        if (outcomeChanged && !enrollment.NeedsReview)
        {
            enrollment.Review = new EnrollmentReview
            {
                Reason = LearningReviewReasons.ResultChangedAfterCompletion,
                ItemId = result.ItemId,
                PreviousScore = null,
                NewScore = item.Score,
                FlaggedUtc = now,
            };
            context.Notifications.Add(() => NotifyAsync(observer => observer.ReviewNeededAsync(enrollment, cancellationToken)));
        }

        await _enrollmentStore.SaveAsync(enrollment, cancellationToken);
    }

    // After completion a result only matters when it undoes what the completion rests on: the item's score was voided,
    // fell, or is no longer a pass. Then the enrollment stays Completed and is flagged for review, once while open.
    private async Task FlagForReviewAsync(Enrollment enrollment, EnrollmentProgress progress, CourseItemResult result, IList<Func<Task>> notifications, CancellationToken cancellationToken)
    {
        var item = progress.Find(result.ItemId);
        var previous = item?.Score;
        var lowered = result.ResetScore
            || result.Passed == false
            || (result.Score is { } score && previous is { } before && score < before);

        if (!lowered || enrollment.NeedsReview)
        {
            return;
        }

        enrollment.Review = new EnrollmentReview
        {
            Reason = LearningReviewReasons.ResultChangedAfterCompletion,
            ItemId = result.ItemId,
            PreviousScore = previous,
            NewScore = result.ResetScore ? null : result.Score,
            FlaggedUtc = _clock.UtcNow,
        };
        enrollment.ModifiedUtc = _clock.UtcNow;

        await _enrollmentStore.SaveAsync(enrollment, cancellationToken);
        notifications.Add(() => NotifyAsync(observer => observer.ReviewNeededAsync(enrollment, cancellationToken)));
    }

    /// <inheritdoc />
    public Task<ProgressUpdate> CompleteItemAsync(Enrollment enrollment, string itemId, string actor, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Task.FromResult(new ProgressUpdate { Enrollment = enrollment, RefusedReason = LearningRefusals.ReasonRequired });
        }

        return UpdateAsync(enrollment, itemId, context =>
        {
            if (context.Item.State != CourseItemState.Completed)
            {
                context.Item.CompletedBy = actor;
                context.Item.CompletionReason = reason.Trim();
            }

            context.Complete = true;

            return null;
        }, cancellationToken, manual: true);
    }

    /// <inheritdoc />
    public Task<ProgressUpdate> ReopenItemAsync(Enrollment enrollment, string itemId, CancellationToken cancellationToken = default)
    {
        if (enrollment is null || string.IsNullOrEmpty(itemId))
        {
            return Task.FromResult(new ProgressUpdate { Enrollment = enrollment, RefusedReason = LearningRefusals.ItemNotFound });
        }

        return _writeGate.RunAsync(
            enrollment.ItemId,
            notifications => ReopenItemCoreAsync(enrollment, itemId, notifications, cancellationToken),
            () => new ProgressUpdate { Enrollment = enrollment, RefusedReason = LearningRefusals.Busy },
            cancellationToken);
    }

    private async Task<ProgressUpdate> ReopenItemCoreAsync(Enrollment enrollment, string itemId, IList<Func<Task>> notifications, CancellationToken cancellationToken)
    {
        enrollment = await _enrollmentStore.FindFreshAsync(enrollment.ItemId, cancellationToken) ?? enrollment;

        var now = _clock.UtcNow;
        var progress = await _progressStore.FindFreshByEnrollmentAsync(enrollment.ItemId, cancellationToken);
        var item = progress?.Find(itemId);

        if (item?.State != CourseItemState.Completed)
        {
            return new ProgressUpdate { Enrollment = enrollment, Progress = progress, Item = item };
        }

        // A completed enrollment is a record: what its completion rests on was taken back, so a person reviews it.
        if (enrollment.Status == EnrollmentStatus.Completed)
        {
            if (!enrollment.NeedsReview)
            {
                enrollment.Review = new EnrollmentReview
                {
                    Reason = LearningReviewReasons.ItemReopenedAfterCompletion,
                    ItemId = itemId,
                    PreviousScore = item.Score,
                    NewScore = item.Score,
                    FlaggedUtc = now,
                };
                enrollment.ModifiedUtc = now;

                await _enrollmentStore.SaveAsync(enrollment, cancellationToken);
                notifications.Add(() => NotifyAsync(observer => observer.ReviewNeededAsync(enrollment, cancellationToken)));
            }

            return new ProgressUpdate { Enrollment = enrollment, Progress = progress, Item = item };
        }

        if (enrollment.Status != EnrollmentStatus.Active)
        {
            return new ProgressUpdate { Enrollment = enrollment, Progress = progress, Item = item, RefusedReason = LearningRefusals.NoAccess };
        }

        var course = await _courseStructure.GetCourseAsync(enrollment.CourseId);

        if (course is null)
        {
            return new ProgressUpdate { Enrollment = enrollment, Progress = progress, Item = item, RefusedReason = LearningRefusals.CourseUnavailable };
        }

        var outline = await _courseStructure.GetOutlineAsync(course);

        item.State = CourseItemState.InProgress;
        item.CompletedUtc = null;
        item.CompletedBy = null;
        item.CompletionReason = null;
        progress.ModifiedUtc = now;
        await _progressStore.SaveAsync(progress, cancellationToken);

        var percent = CourseCompletionEvaluator.ComputePercent(outline, progress);
        var grade = CourseGradeCalculator.Compute(outline, progress);
        var passed = CourseGradeCalculator.IsPassed(grade, course.Get<CoursePart>(nameof(CoursePart))?.PassMark);

        if (percent != enrollment.ProgressPercent || grade != enrollment.Grade || passed != enrollment.Passed)
        {
            enrollment.ProgressPercent = percent;
            enrollment.Grade = grade;
            enrollment.Passed = passed;
            enrollment.ModifiedUtc = now;
            await _enrollmentStore.SaveAsync(enrollment, cancellationToken);
        }

        return new ProgressUpdate { Enrollment = enrollment, Progress = progress, Item = item };
    }

    /// <inheritdoc />
    public Task<ProgressUpdate> ApproveCompletionAsync(Enrollment enrollment, string actor, CancellationToken cancellationToken = default)
    {
        if (enrollment is null)
        {
            return Task.FromResult(new ProgressUpdate { RefusedReason = LearningRefusals.NotActive });
        }

        return _writeGate.RunAsync(
            enrollment.ItemId,
            notifications => ApproveCompletionCoreAsync(enrollment, actor, notifications, cancellationToken),
            () => new ProgressUpdate { Enrollment = enrollment, RefusedReason = LearningRefusals.Busy },
            cancellationToken);
    }

    private async Task<ProgressUpdate> ApproveCompletionCoreAsync(Enrollment enrollment, string actor, IList<Func<Task>> notifications, CancellationToken cancellationToken)
    {
        enrollment = await _enrollmentStore.FindFreshAsync(enrollment.ItemId, cancellationToken);

        if (enrollment is null || enrollment.Status != EnrollmentStatus.Active)
        {
            return new ProgressUpdate { Enrollment = enrollment, RefusedReason = LearningRefusals.NotActive };
        }

        var course = await _courseStructure.GetCourseAsync(enrollment.CourseId);

        if (course is null)
        {
            return new ProgressUpdate { Enrollment = enrollment, RefusedReason = LearningRefusals.CourseUnavailable };
        }

        var now = _clock.UtcNow;
        var outline = await _courseStructure.GetOutlineAsync(course);
        var progress = await _progressStore.FindFreshByEnrollmentAsync(enrollment.ItemId, cancellationToken) ?? new EnrollmentProgress { EnrollmentId = enrollment.ItemId };

        enrollment.Properties[LearningRuleNames.StaffApprovalProperty] = new Dictionary<string, object>
        {
            ["Actor"] = actor,
            ["ApprovedUtc"] = now,
        };

        var grade = CourseGradeCalculator.Compute(outline, progress);
        var passed = CourseGradeCalculator.IsPassed(grade, course.Get<CoursePart>(nameof(CoursePart))?.PassMark);
        var completed = await _completionEvaluator.IsCompleteAsync(outline, enrollment, progress, grade);

        if (completed)
        {
            Complete(enrollment, outline, progress, now, grade, passed, actor);
        }

        enrollment.ModifiedUtc = now;
        await _enrollmentStore.SaveAsync(enrollment, cancellationToken);

        if (completed)
        {
            notifications.Add(() => NotifyAsync(observer => observer.StatusChangedAsync(enrollment, EnrollmentStatus.Active, cancellationToken)));
            notifications.Add(() => NotifyAsync(observer => observer.CompletedAsync(enrollment, cancellationToken)));
        }

        return new ProgressUpdate { Enrollment = enrollment, Progress = progress, CourseCompleted = completed };
    }

    private static void Complete(Enrollment enrollment, CourseOutline outline, EnrollmentProgress progress, DateTime now, decimal? grade, bool? passed, string actor)
    {
        enrollment.Grade = grade;
        enrollment.Passed = passed;
        enrollment.ProgressPercent = CourseCompletionEvaluator.ComputePercent(outline, progress);
        enrollment.CompletedUtc = now;
        enrollment.Completion = CompletionSnapshotFactory.Create(outline, progress, now, grade, passed);
        enrollment.History.Add(new EnrollmentStatusChange
        {
            From = EnrollmentStatus.Active,
            To = EnrollmentStatus.Completed,
            ChangedUtc = now,
            Actor = actor,
        });

        enrollment.Status = EnrollmentStatus.Completed;
    }

    private Task<ProgressUpdate> UpdateAsync(Enrollment enrollment, string itemId, Func<UpdateContext, string> apply, CancellationToken cancellationToken, bool manual = false, Func<CompletedContext, Task> whenCompleted = null)
    {
        if (enrollment is null || string.IsNullOrEmpty(itemId))
        {
            return Task.FromResult(new ProgressUpdate { Enrollment = enrollment, RefusedReason = LearningRefusals.ItemNotFound });
        }

        return _writeGate.RunAsync(
            enrollment.ItemId,
            notifications => UpdateCoreAsync(enrollment, itemId, apply, notifications, manual, cancellationToken, whenCompleted),
            () => new ProgressUpdate { Enrollment = enrollment, RefusedReason = LearningRefusals.Busy },
            cancellationToken);
    }

    private async Task<ProgressUpdate> UpdateCoreAsync(Enrollment enrollment, string itemId, Func<UpdateContext, string> apply, IList<Func<Task>> notifications, bool manual, CancellationToken cancellationToken, Func<CompletedContext, Task> whenCompleted = null)
    {
        // Read the enrollment again inside the lock: a caller may hold a copy read earlier or in another scope, and
        // deciding on that copy could complete the course twice or save a second document.
        enrollment = await _enrollmentStore.FindFreshAsync(enrollment.ItemId, cancellationToken) ?? enrollment;

        var now = _clock.UtcNow;
        var course = await _courseStructure.GetCourseAsync(enrollment.CourseId);

        if (course is null)
        {
            return new ProgressUpdate { Enrollment = enrollment, RefusedReason = LearningRefusals.CourseUnavailable };
        }

        var outline = await _courseStructure.GetOutlineAsync(course);
        var entry = outline.Find(itemId);

        if (entry is null)
        {
            return new ProgressUpdate { Enrollment = enrollment, RefusedReason = LearningRefusals.ItemNotFound };
        }

        var progress = await _progressStore.FindFreshByEnrollmentAsync(enrollment.ItemId, cancellationToken);
        var isNewProgress = progress is null;

        progress ??= new EnrollmentProgress
        {
            ItemId = UniqueId.GenerateId(),
            EnrollmentId = enrollment.ItemId,
            UserId = enrollment.UserId,
            CourseId = enrollment.CourseId,
        };

        if (enrollment.Status == EnrollmentStatus.Completed)
        {
            // A completed enrollment is a record: the learner may review the course, but nothing changes. A result that
            // changed after completion only flags the enrollment for a person to review.
            if (whenCompleted is not null)
            {
                await whenCompleted(new CompletedContext(enrollment, progress, isNewProgress, outline, course, notifications));
            }

            return new ProgressUpdate { Enrollment = enrollment, Progress = progress, Item = progress.Find(itemId) };
        }

        if (manual ? enrollment.Status != EnrollmentStatus.Active : !EnrollmentAccess.HasAccess(enrollment, now))
        {
            return new ProgressUpdate { Enrollment = enrollment, Progress = progress, RefusedReason = LearningRefusals.NoAccess };
        }

        if (!manual)
        {
            var release = await _releaseEvaluator.EvaluateAsync(entry, enrollment, progress);

            if (!release.IsReleased)
            {
                return new ProgressUpdate { Enrollment = enrollment, Progress = progress, RefusedReason = release.Reason ?? LearningRefusals.NotAvailableYet };
            }
        }

        var courseStarted = progress.Items.Any(candidate => candidate.FirstAccessUtc is not null);
        var item = progress.Find(itemId);
        var itemStarted = item?.FirstAccessUtc is not null;

        if (item is null)
        {
            item = new CourseItemProgress { ContentItemId = itemId, State = CourseItemState.NotStarted };
            progress.Items.Add(item);
        }

        var context = new UpdateContext
        {
            Entry = entry,
            Item = item,
            UtcNow = now,
            LastAccessUtc = progress.Items.Max(candidate => candidate.LastAccessUtc),
        };

        var refused = apply(context);

        if (refused is not null)
        {
            if (isNewProgress && !itemStarted)
            {
                progress.Items.Remove(item);
            }

            return new ProgressUpdate { Enrollment = enrollment, Progress = progress, Item = isNewProgress ? null : item, RefusedReason = refused };
        }

        item.FirstAccessUtc ??= now;
        item.LastAccessUtc = now;

        if (item.State == CourseItemState.NotStarted)
        {
            item.State = CourseItemState.InProgress;
        }

        var wasCompleted = item.State == CourseItemState.Completed;

        if (!wasCompleted)
        {
            ApplyCompletionMode(context);
        }

        var itemCompleted = !wasCompleted && item.State == CourseItemState.Completed;

        if (itemCompleted)
        {
            item.CompletedUtc = now;
        }

        progress.ModifiedUtc = now;

        if (isNewProgress)
        {
            await _progressStore.CreateAsync(progress, cancellationToken);
        }
        else
        {
            await _progressStore.SaveAsync(progress, cancellationToken);
        }

        var enrollmentChanged = false;

        if (enrollment.StartedUtc is null)
        {
            enrollment.StartedUtc = now;
            enrollmentChanged = true;
        }

        if (enrollment.LastActivityUtc is null || now - enrollment.LastActivityUtc >= _activityWriteInterval)
        {
            enrollment.LastActivityUtc = now;
            enrollmentChanged = true;
        }

        var percent = CourseCompletionEvaluator.ComputePercent(outline, progress);
        var grade = CourseGradeCalculator.Compute(outline, progress);
        var passed = CourseGradeCalculator.IsPassed(grade, course.Get<CoursePart>(nameof(CoursePart))?.PassMark);

        if (percent != enrollment.ProgressPercent || grade != enrollment.Grade || passed != enrollment.Passed)
        {
            enrollment.ProgressPercent = percent;
            enrollment.Grade = grade;
            enrollment.Passed = passed;
            enrollmentChanged = true;
        }

        var courseCompleted = false;

        if (enrollment.Status == EnrollmentStatus.Active && await _completionEvaluator.IsCompleteAsync(outline, enrollment, progress, grade))
        {
            Complete(enrollment, outline, progress, now, grade, passed, manual ? context.Result?.RecordedBy ?? context.Item.CompletedBy : enrollment.UserId);
            enrollmentChanged = true;
            courseCompleted = true;
        }

        if (enrollmentChanged)
        {
            enrollment.ModifiedUtc = now;
            await _enrollmentStore.SaveAsync(enrollment, cancellationToken);
        }

        var eventContext = new CourseItemEventContext
        {
            Enrollment = enrollment,
            Item = entry.Item,
            Kind = entry.Part?.Kind,
            Progress = item,
        };

        if (!courseStarted)
        {
            notifications.Add(() => NotifyAsync(observer => observer.CourseStartedAsync(enrollment, cancellationToken)));
        }

        if (!itemStarted)
        {
            notifications.Add(() => HandleAsync(handler => handler.StartedAsync(eventContext)));
        }

        if (itemCompleted)
        {
            notifications.Add(() => NotifyAsync(observer => observer.ItemCompletedAsync(enrollment, item, cancellationToken)));
            notifications.Add(() => HandleAsync(handler => handler.CompletedAsync(eventContext)));
        }

        if (courseCompleted)
        {
            notifications.Add(() => NotifyAsync(observer => observer.StatusChangedAsync(enrollment, EnrollmentStatus.Active, cancellationToken)));
            notifications.Add(() => NotifyAsync(observer => observer.CompletedAsync(enrollment, cancellationToken)));
        }

        return new ProgressUpdate
        {
            Enrollment = enrollment,
            Progress = progress,
            Item = item,
            ItemCompleted = itemCompleted,
            CourseCompleted = courseCompleted,
        };
    }

    private async Task<Enrollment> TrackedAsync(Enrollment enrollment, CancellationToken cancellationToken)
        => string.IsNullOrEmpty(enrollment.ItemId) ? enrollment : await _enrollmentStore.FindByIdAsync(enrollment.ItemId, cancellationToken) ?? enrollment;

    private static void ApplyCompletionMode(UpdateContext context)
    {
        var item = context.Item;
        var part = context.Entry.Part;
        var result = context.Result;

        if (context.Complete)
        {
            item.State = CourseItemState.Completed;

            return;
        }

        switch (part?.CompletionMode ?? CompletionModes.Viewed)
        {
            case CompletionModes.Viewed:
                item.State = CourseItemState.Completed;
                break;

            case CompletionModes.MinimumTime:
                if (item.SecondsSpent >= (part.MinimumMinutes ?? 0) * 60L)
                {
                    item.State = CourseItemState.Completed;
                }

                break;

            case CompletionModes.Watched:
                var required = context.Entry.Item.Get<VideoLessonPart>(nameof(VideoLessonPart))?.RequiredPercent ?? 90;

                if (item.PercentViewed >= Math.Clamp(required, 1, 100))
                {
                    item.State = CourseItemState.Completed;
                }

                break;

            case CompletionModes.Acknowledged:
                if (item.AttestedUtc is not null)
                {
                    item.State = CourseItemState.Completed;
                }

                break;

            case CompletionModes.Submitted:
                if (result is { Submitted: true } or { Graded: true })
                {
                    item.State = CourseItemState.Completed;
                }

                break;

            case CompletionModes.Graded:
                if (result is { Graded: true })
                {
                    item.State = CourseItemState.Completed;
                }

                break;

            case CompletionModes.Passed:
                if (item.Passed == true)
                {
                    item.State = CourseItemState.Completed;
                }
                else if (item.Passed == false)
                {
                    item.State = CourseItemState.Failed;
                }

                break;

            case CompletionModes.Attended:
                if (result is { Attended: true })
                {
                    item.State = CourseItemState.Completed;
                }

                break;

            case CompletionModes.PackageReported:
                if (result is { ReportedComplete: true })
                {
                    item.State = CourseItemState.Completed;
                }

                break;
        }
    }

    private async Task NotifyAsync(Func<IEnrollmentObserver, Task> notify)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await notify(observer);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "An enrollment observer {Observer} failed.", observer.GetType().Name);
            }
        }
    }

    private async Task HandleAsync(Func<ICourseItemHandler, Task> handle)
    {
        foreach (var handler in _itemHandlers)
        {
            try
            {
                await handle(handler);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "A course item handler {Handler} failed.", handler.GetType().Name);
            }
        }
    }

    private sealed class UpdateContext
    {
        public DateTime? LastAccessUtc { get; init; }

        public CourseOutlineEntry Entry { get; init; }

        public CourseItemProgress Item { get; init; }

        public DateTime UtcNow { get; init; }

        public bool Complete { get; set; }

        public CourseItemResult Result { get; set; }
    }

    private sealed record CompletedContext(Enrollment Enrollment, EnrollmentProgress Progress, bool IsNewProgress, CourseOutline Outline, ContentItem Course, IList<Func<Task>> Notifications);
}

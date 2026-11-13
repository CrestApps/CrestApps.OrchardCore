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

    private static readonly TimeSpan _activityWriteInterval = TimeSpan.FromHours(1);

    private readonly EnrollmentStore _enrollmentStore;
    private readonly EnrollmentProgressStore _progressStore;
    private readonly ICourseStructureService _courseStructure;
    private readonly CourseReleaseEvaluator _releaseEvaluator;
    private readonly CourseCompletionEvaluator _completionEvaluator;
    private readonly IEnumerable<IEnrollmentObserver> _observers;
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
            context.Item.SecondsSpent += Math.Clamp(secondsSpent, 0, MaxSecondsPerCall);

            return null;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ProgressUpdate> RecordViewedAsync(Enrollment enrollment, string itemId, int percentViewed, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(enrollment, itemId, context =>
        {
            context.Item.PercentViewed = Math.Max(context.Item.PercentViewed, Math.Clamp(percentViewed, 0, 100));

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
                return "This item is not completed by marking it done.";
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
                return "This item has no statement to confirm.";
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

            if (result.Score is not null)
            {
                item.Score = Math.Clamp(result.Score.Value, 0, 100);
            }

            item.Attempts = Math.Max(item.Attempts, result.Attempts);
            item.SelfReported = result.SelfReported;

            var passMark = context.Entry.Part?.PassMark;

            item.Passed = result.Passed ?? (passMark is not null && item.Score is not null ? item.Score >= passMark : item.Passed);
            context.Result = result;

            return null;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ProgressUpdate> CompleteItemAsync(Enrollment enrollment, string itemId, string actor, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Task.FromResult(new ProgressUpdate { Enrollment = enrollment, RefusedReason = "A reason is required." });
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
    public async Task<ProgressUpdate> ApproveCompletionAsync(Enrollment enrollment, string actor, CancellationToken cancellationToken = default)
    {
        if (enrollment is null || enrollment.Status != EnrollmentStatus.Active)
        {
            return new ProgressUpdate { Enrollment = enrollment, RefusedReason = "Only an active enrollment can be approved." };
        }

        var course = await _courseStructure.GetCourseAsync(enrollment.CourseId);

        if (course is null)
        {
            return new ProgressUpdate { Enrollment = enrollment, RefusedReason = "The course is not available." };
        }

        var now = _clock.UtcNow;
        var outline = await _courseStructure.GetOutlineAsync(course);
        var progress = await _progressStore.FindByEnrollmentAsync(enrollment.ItemId, cancellationToken) ?? new EnrollmentProgress { EnrollmentId = enrollment.ItemId };

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
            await NotifyAsync(observer => observer.StatusChangedAsync(enrollment, EnrollmentStatus.Active, cancellationToken));
            await NotifyAsync(observer => observer.CompletedAsync(enrollment, cancellationToken));
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

    private async Task<ProgressUpdate> UpdateAsync(Enrollment enrollment, string itemId, Func<UpdateContext, string> apply, CancellationToken cancellationToken, bool manual = false)
    {
        if (enrollment is null || string.IsNullOrEmpty(itemId))
        {
            return new ProgressUpdate { Enrollment = enrollment, RefusedReason = "The course item was not found." };
        }

        var now = _clock.UtcNow;
        var course = await _courseStructure.GetCourseAsync(enrollment.CourseId);

        if (course is null)
        {
            return new ProgressUpdate { Enrollment = enrollment, RefusedReason = "The course is not available." };
        }

        var outline = await _courseStructure.GetOutlineAsync(course);
        var entry = outline.Find(itemId);

        if (entry is null)
        {
            return new ProgressUpdate { Enrollment = enrollment, RefusedReason = "The course item was not found." };
        }

        var progress = await _progressStore.FindByEnrollmentAsync(enrollment.ItemId, cancellationToken);
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
            // A completed enrollment is a record: the learner may review the course, but nothing changes.
            return new ProgressUpdate { Enrollment = enrollment, Progress = progress, Item = progress.Find(itemId) };
        }

        if (manual ? enrollment.Status != EnrollmentStatus.Active : !EnrollmentAccess.HasAccess(enrollment, now))
        {
            return new ProgressUpdate { Enrollment = enrollment, Progress = progress, RefusedReason = "You do not have access to this course." };
        }

        if (!manual)
        {
            var release = await _releaseEvaluator.EvaluateAsync(entry, enrollment, progress);

            if (!release.IsReleased)
            {
                return new ProgressUpdate { Enrollment = enrollment, Progress = progress, RefusedReason = release.Reason ?? "This item is not available yet." };
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
            Complete(enrollment, outline, progress, now, grade, passed, manual ? context.Item.CompletedBy : enrollment.UserId);
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
            await NotifyAsync(observer => observer.CourseStartedAsync(enrollment, cancellationToken));
        }

        if (!itemStarted)
        {
            await HandleAsync(handler => handler.StartedAsync(eventContext));
        }

        if (itemCompleted)
        {
            await NotifyAsync(observer => observer.ItemCompletedAsync(enrollment, item, cancellationToken));
            await HandleAsync(handler => handler.CompletedAsync(eventContext));
        }

        if (courseCompleted)
        {
            await NotifyAsync(observer => observer.StatusChangedAsync(enrollment, EnrollmentStatus.Active, cancellationToken));
            await NotifyAsync(observer => observer.CompletedAsync(enrollment, cancellationToken));
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
        public CourseOutlineEntry Entry { get; init; }

        public CourseItemProgress Item { get; init; }

        public DateTime UtcNow { get; init; }

        public bool Complete { get; set; }

        public CourseItemResult Result { get; set; }
    }
}

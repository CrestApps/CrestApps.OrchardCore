using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Numbering;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Enrolls learners and moves enrollments through their states, under a lock per learner and course so that a learner
/// never holds two open enrollments in one course and offering.
/// </summary>
public sealed class DefaultEnrollmentService : IEnrollmentService
{
    /// <summary>
    /// The actor recorded for changes Learning makes on its own, such as expiring access.
    /// </summary>
    public const string SystemActor = "Learning";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly EnrollmentStore _enrollmentStore;
    private readonly EnrollmentProgressStore _progressStore;
    private readonly ICourseStructureService _courseStructure;
    private readonly IEnumerable<ICoursePrerequisiteEvaluator> _prerequisiteEvaluators;
    private readonly IEnumerable<IEnrollmentObserver> _observers;
    private readonly INumberSequence _numberSequence;
    private readonly IDistributedLock _distributedLock;
    private readonly EnrollmentWriteGate _writeGate;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultEnrollmentService"/> class.
    /// </summary>
    /// <param name="enrollmentStore">The enrollment store.</param>
    /// <param name="progressStore">The progress store.</param>
    /// <param name="courseStructure">The course structure reader.</param>
    /// <param name="prerequisiteEvaluators">The prerequisite evaluators.</param>
    /// <param name="observers">The enrollment observers.</param>
    /// <param name="numberSequence">The number sequence for enrollment numbers.</param>
    /// <param name="distributedLock">The distributed lock.</param>
    /// <param name="writeGate">The per-enrollment write lock.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public DefaultEnrollmentService(
        EnrollmentStore enrollmentStore,
        EnrollmentProgressStore progressStore,
        ICourseStructureService courseStructure,
        IEnumerable<ICoursePrerequisiteEvaluator> prerequisiteEvaluators,
        IEnumerable<IEnrollmentObserver> observers,
        INumberSequence numberSequence,
        IDistributedLock distributedLock,
        EnrollmentWriteGate writeGate,
        IClock clock,
        ILogger<DefaultEnrollmentService> logger)
    {
        _enrollmentStore = enrollmentStore;
        _progressStore = progressStore;
        _courseStructure = courseStructure;
        _prerequisiteEvaluators = prerequisiteEvaluators;
        _observers = observers;
        _numberSequence = numberSequence;
        _distributedLock = distributedLock;
        _writeGate = writeGate;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EnrollmentResult> EnrollAsync(EnrollmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.CourseId) || string.IsNullOrWhiteSpace(request.Source))
        {
            return EnrollmentResult.Fail(EnrollmentErrors.Invalid);
        }

        var course = await _courseStructure.GetCourseAsync(request.CourseId);

        if (course is null)
        {
            return EnrollmentResult.Fail(EnrollmentErrors.CourseNotFound);
        }

        var offeringId = string.IsNullOrWhiteSpace(request.OfferingId) ? null : request.OfferingId;
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync($"Learning:Enrollment:{request.UserId}:{request.CourseId}:{offeringId}", _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return EnrollmentResult.Fail(EnrollmentErrors.Busy);
        }

        var notifications = new List<Func<Task>>();
        EnrollmentResult created;

        await using (locker)
        {
            var open = await _enrollmentStore.FindOpenAsync(request.UserId, request.CourseId, offeringId, cancellationToken);

            if (open is not null)
            {
                // A change to an existing enrollment goes through its own lock, like every other write to it; the gate
                // commits and tells the observers after the commit.
                return await WriteAsync(open.ItemId, (enrollment, queued) => AddGrantAsync(enrollment, request, _clock.UtcNow, queued, cancellationToken), cancellationToken);
            }

            if (!request.IgnorePrerequisites && !await MeetsPrerequisitesAsync(request.UserId, course.Get<CoursePart>(nameof(CoursePart)), cancellationToken))
            {
                return EnrollmentResult.Fail(EnrollmentErrors.PrerequisitesNotMet);
            }

            var now = _clock.UtcNow;
            var previous = await FindPreviousAsync(request, cancellationToken);
            var number = await _numberSequence.NextAsync(LearningConstants.EnrollmentNumberSeries, cancellationToken: cancellationToken);
            var enrollment = new Enrollment
            {
                ItemId = UniqueId.GenerateId(),
                UserId = request.UserId,
                CourseId = request.CourseId,
                OfferingId = offeringId,
                Status = request.RequiresApproval ? EnrollmentStatus.Pending : EnrollmentStatus.Active,
                Number = "E" + number.ToString("D6", CultureInfo.InvariantCulture),
                CreatedUtc = now,
                ModifiedUtc = now,
                DueUtc = request.DueUtc,
                Cycle = (previous?.Cycle ?? 0) + 1,
                PreviousEnrollmentId = previous?.ItemId,
                Grants =
                [
                    new EnrollmentGrant
                    {
                        Source = request.Source,
                        SourceId = request.SourceId,
                        GrantedUtc = now,
                        AccessEndsUtc = request.AccessEndsUtc,
                    },
                ],
            };

            enrollment.AccessEndsUtc = EnrollmentAccess.ComputeAccessEnds(enrollment.Grants);

            await _enrollmentStore.CreateAsync(enrollment, cancellationToken);

            // Commit before the lock is let go, so a second request for the same learner and course finds this one.
            await _writeGate.CommitAsync(cancellationToken);

            notifications.Add(enrollment.Status == EnrollmentStatus.Pending
                ? () => NotifyAsync(observer => observer.RequestedAsync(enrollment, cancellationToken))
                : () => NotifyAsync(observer => observer.EnrolledAsync(enrollment, cancellationToken)));

            created = new EnrollmentResult
            {
                Enrollment = enrollment,
                Created = true,
                Changed = true,
            };
        }

        foreach (var notify in notifications)
        {
            await notify();
        }

        return created;
    }

    /// <inheritdoc />
    public Task<EnrollmentResult> ApproveAsync(string enrollmentId, string actor, CancellationToken cancellationToken = default)
        => WriteAsync(enrollmentId, async (enrollment, notifications) =>
        {
            if (enrollment.Status != EnrollmentStatus.Pending)
            {
                return EnrollmentResult.Fail(EnrollmentErrors.InvalidTransition);
            }

            Move(enrollment, EnrollmentStatus.Active, actor, null);
            await _enrollmentStore.SaveAsync(enrollment, cancellationToken);
            notifications.Add(() => NotifyAsync(observer => observer.StatusChangedAsync(enrollment, EnrollmentStatus.Pending, cancellationToken)));
            notifications.Add(() => NotifyAsync(observer => observer.EnrolledAsync(enrollment, cancellationToken)));

            return new EnrollmentResult { Enrollment = enrollment, Changed = true };
        }, cancellationToken);

    /// <inheritdoc />
    public Task<EnrollmentResult> RejectAsync(string enrollmentId, string actor, string reason, CancellationToken cancellationToken = default)
        => WriteAsync(enrollmentId, (enrollment, notifications) => enrollment.Status != EnrollmentStatus.Pending
            ? Task.FromResult(EnrollmentResult.Fail(EnrollmentErrors.InvalidTransition))
            : ChangeStatusCoreAsync(enrollment, EnrollmentStatus.Withdrawn, actor, reason, notifications, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<EnrollmentResult> ChangeStatusAsync(string enrollmentId, EnrollmentStatus status, string actor, string reason, CancellationToken cancellationToken = default)
        => WriteAsync(enrollmentId, (enrollment, notifications) => ChangeStatusCoreAsync(enrollment, status, actor, reason, notifications, cancellationToken), cancellationToken);

    private async Task<EnrollmentResult> ChangeStatusCoreAsync(Enrollment enrollment, EnrollmentStatus status, string actor, string reason, IList<Func<Task>> notifications, CancellationToken cancellationToken)
    {
        if (!EnrollmentTransitions.CanMove(enrollment.Status, status))
        {
            return EnrollmentResult.Fail(EnrollmentErrors.InvalidTransition);
        }

        if ((status is EnrollmentStatus.Completed or EnrollmentStatus.Suspended or EnrollmentStatus.Withdrawn) && string.IsNullOrWhiteSpace(reason))
        {
            return EnrollmentResult.Fail(EnrollmentErrors.ReasonRequired);
        }

        var previous = enrollment.Status;

        if (status == EnrollmentStatus.Completed)
        {
            await CompleteAsync(enrollment, actor, reason.Trim(), cancellationToken);
        }
        else
        {
            Move(enrollment, status, actor, reason?.Trim());
        }

        await _enrollmentStore.SaveAsync(enrollment, cancellationToken);
        notifications.Add(() => NotifyAsync(observer => observer.StatusChangedAsync(enrollment, previous, cancellationToken)));

        if (status == EnrollmentStatus.Completed)
        {
            notifications.Add(() => NotifyAsync(observer => observer.CompletedAsync(enrollment, cancellationToken)));
        }

        return new EnrollmentResult { Enrollment = enrollment, Changed = true };
    }

    /// <inheritdoc />
    public Task<EnrollmentResult> ExtendAsync(string enrollmentId, DateTime? accessEndsUtc, DateTime? dueUtc, string actor, string reason, CancellationToken cancellationToken = default)
        => WriteAsync(enrollmentId, async (enrollment, notifications) =>
        {
            if (enrollment.Status is EnrollmentStatus.Completed or EnrollmentStatus.Withdrawn)
            {
                return EnrollmentResult.Fail(EnrollmentErrors.InvalidTransition);
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return EnrollmentResult.Fail(EnrollmentErrors.ReasonRequired);
            }

            var now = _clock.UtcNow;

            if (accessEndsUtc is not null)
            {
                enrollment.Grants.Add(new EnrollmentGrant
                {
                    Source = LearningConstants.EnrollmentSources.Manual,
                    SourceId = "Extension:" + UniqueId.GenerateId(),
                    GrantedUtc = now,
                    AccessEndsUtc = DateTime.SpecifyKind(accessEndsUtc.Value, DateTimeKind.Utc),
                });

                enrollment.AccessEndsUtc = EnrollmentAccess.ComputeAccessEnds(enrollment.Grants);
            }

            if (dueUtc is not null)
            {
                enrollment.DueUtc = DateTime.SpecifyKind(dueUtc.Value, DateTimeKind.Utc);
            }

            enrollment.ModifiedUtc = now;

            var previous = enrollment.Status;

            if (previous == EnrollmentStatus.Expired && (enrollment.AccessEndsUtc is null || enrollment.AccessEndsUtc > now))
            {
                Move(enrollment, EnrollmentStatus.Active, actor, reason.Trim());
            }

            await _enrollmentStore.SaveAsync(enrollment, cancellationToken);

            if (previous != enrollment.Status)
            {
                notifications.Add(() => NotifyAsync(observer => observer.StatusChangedAsync(enrollment, previous, cancellationToken)));
            }

            notifications.Add(() => NotifyAsync(observer => observer.ExtendedAsync(enrollment, actor, reason.Trim(), cancellationToken)));

            return new EnrollmentResult { Enrollment = enrollment, Changed = true };
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Enrollment>> RevokeGrantAsync(string source, string sourceId, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var changed = new List<Enrollment>();
        var ids = (await _enrollmentStore.ListByGrantAsync(source, sourceId, cancellationToken)).Select(enrollment => enrollment.ItemId).ToList();

        foreach (var id in ids)
        {
            var result = await WriteAsync(id, async (enrollment, notifications) =>
            {
                if (!enrollment.IsOpen && enrollment.Status != EnrollmentStatus.Expired)
                {
                    return EnrollmentResult.Fail(EnrollmentErrors.InvalidTransition);
                }

                var removed = 0;

                for (var index = enrollment.Grants.Count - 1; index >= 0; index--)
                {
                    var grant = enrollment.Grants[index];

                    if (string.Equals(grant.Source, source, StringComparison.Ordinal) && string.Equals(grant.SourceId, sourceId, StringComparison.Ordinal))
                    {
                        enrollment.Grants.RemoveAt(index);
                        removed++;
                    }
                }

                if (removed == 0)
                {
                    return EnrollmentResult.Fail(EnrollmentErrors.NotFound);
                }

                enrollment.AccessEndsUtc = EnrollmentAccess.ComputeAccessEnds(enrollment.Grants);
                enrollment.ModifiedUtc = _clock.UtcNow;

                var previous = enrollment.Status;

                if (enrollment.Grants.Count == 0)
                {
                    Move(enrollment, EnrollmentStatus.Withdrawn, actor, reason);
                }

                await _enrollmentStore.SaveAsync(enrollment, cancellationToken);

                if (previous != enrollment.Status)
                {
                    notifications.Add(() => NotifyAsync(observer => observer.StatusChangedAsync(enrollment, previous, cancellationToken)));
                }

                return new EnrollmentResult { Enrollment = enrollment, Changed = true };
            }, cancellationToken);

            if (result.Changed)
            {
                changed.Add(result.Enrollment);
            }
        }

        return changed;
    }

    /// <inheritdoc />
    public async Task<int> ExpireEndedAsync(CancellationToken cancellationToken = default)
    {
        const int PageSize = 100;

        var now = _clock.UtcNow;
        var expired = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (!cancellationToken.IsCancellationRequested)
        {
            var page = await _enrollmentStore.ListEndedAsync(now, PageSize, cancellationToken);
            var moved = false;

            foreach (var id in page.Select(enrollment => enrollment.ItemId).ToList())
            {
                if (!seen.Add(id))
                {
                    continue;
                }

                var result = await WriteAsync(id, async (enrollment, notifications) =>
                {
                    if (!EnrollmentTransitions.CanMove(enrollment.Status, EnrollmentStatus.Expired) || !(enrollment.AccessEndsUtc <= now))
                    {
                        return EnrollmentResult.Fail(EnrollmentErrors.InvalidTransition);
                    }

                    var previous = enrollment.Status;

                    Move(enrollment, EnrollmentStatus.Expired, SystemActor, "Access ended");
                    await _enrollmentStore.SaveAsync(enrollment, cancellationToken);
                    notifications.Add(() => NotifyAsync(observer => observer.StatusChangedAsync(enrollment, previous, cancellationToken)));

                    return new EnrollmentResult { Enrollment = enrollment, Changed = true };
                }, cancellationToken);

                if (result.Changed)
                {
                    expired++;
                    moved = true;
                }
            }

            if (!moved || page.Count < PageSize)
            {
                break;
            }
        }

        return expired;
    }

    /// <inheritdoc />
    public Task<EnrollmentResult> ClearReviewAsync(string enrollmentId, string actor, string note, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return Task.FromResult(EnrollmentResult.Fail(EnrollmentErrors.ReasonRequired));
        }

        return WriteAsync(enrollmentId, async (enrollment, notifications) =>
        {
            if (!enrollment.NeedsReview)
            {
                return new EnrollmentResult { Enrollment = enrollment };
            }

            enrollment.Review.ClearedUtc = _clock.UtcNow;
            enrollment.Review.ClearedBy = actor;
            enrollment.Review.ClearNote = note.Trim();
            enrollment.ModifiedUtc = _clock.UtcNow;
            await _enrollmentStore.SaveAsync(enrollment, cancellationToken);

            return new EnrollmentResult { Enrollment = enrollment, Changed = true };
        }, cancellationToken);
    }

    private Task<EnrollmentResult> WriteAsync(string enrollmentId, Func<Enrollment, IList<Func<Task>>, Task<EnrollmentResult>> work, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(enrollmentId))
        {
            return Task.FromResult(EnrollmentResult.Fail(EnrollmentErrors.NotFound));
        }

        return _writeGate.RunAsync(
            enrollmentId,
            async notifications =>
            {
                var enrollment = await _enrollmentStore.FindFreshAsync(enrollmentId, cancellationToken);

                return enrollment is null ? EnrollmentResult.Fail(EnrollmentErrors.NotFound) : await work(enrollment, notifications);
            },
            () => EnrollmentResult.Fail(EnrollmentErrors.Busy),
            cancellationToken);
    }

    /// <summary>
    /// Completes an enrollment: its state, its completion time and its completion snapshot.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="actor">Who completed it.</param>
    /// <param name="reason">Why, when completed by hand.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    internal async Task CompleteAsync(Enrollment enrollment, string actor, string reason, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var course = await _courseStructure.GetCourseAsync(enrollment.CourseId);
        var outline = course is null ? new CourseOutline() : await _courseStructure.GetOutlineAsync(course);
        var progress = await _progressStore.FindByEnrollmentAsync(enrollment.ItemId, cancellationToken);
        var grade = CourseGradeCalculator.Compute(outline, progress);
        var passed = CourseGradeCalculator.IsPassed(grade, course?.Get<CoursePart>(nameof(CoursePart))?.PassMark);

        enrollment.Grade = grade;
        enrollment.Passed = passed;
        enrollment.CompletedUtc = now;
        enrollment.ProgressPercent = CourseCompletionEvaluator.ComputePercent(outline, progress);
        enrollment.Completion = CompletionSnapshotFactory.Create(outline, progress, now, grade, passed, reason is null ? null : actor, reason);

        Move(enrollment, EnrollmentStatus.Completed, actor, reason);
    }

    private async Task<EnrollmentResult> AddGrantAsync(Enrollment open, EnrollmentRequest request, DateTime now, IList<Func<Task>> notifications, CancellationToken cancellationToken)
    {
        if (open.Grants.Any(grant => string.Equals(grant.Source, request.Source, StringComparison.Ordinal) && string.Equals(grant.SourceId, request.SourceId, StringComparison.Ordinal)))
        {
            return new EnrollmentResult { Enrollment = open };
        }

        open.Grants.Add(new EnrollmentGrant
        {
            Source = request.Source,
            SourceId = request.SourceId,
            GrantedUtc = now,
            AccessEndsUtc = request.AccessEndsUtc,
        });

        open.AccessEndsUtc = EnrollmentAccess.ComputeAccessEnds(open.Grants);
        open.ModifiedUtc = now;

        if (request.DueUtc is not null && (open.DueUtc is null || request.DueUtc < open.DueUtc))
        {
            open.DueUtc = request.DueUtc;
        }

        var previous = open.Status;

        if (previous == EnrollmentStatus.Pending && !request.RequiresApproval)
        {
            Move(open, EnrollmentStatus.Active, request.Actor ?? request.Source, null);
        }

        await _enrollmentStore.SaveAsync(open, cancellationToken);

        if (previous != open.Status)
        {
            notifications.Add(() => NotifyAsync(observer => observer.StatusChangedAsync(open, previous, cancellationToken)));
            notifications.Add(() => NotifyAsync(observer => observer.EnrolledAsync(open, cancellationToken)));
        }

        return new EnrollmentResult { Enrollment = open, Changed = true };
    }

    private Task<bool> MeetsPrerequisitesAsync(string userId, CoursePart part, CancellationToken cancellationToken)
        => CoursePrerequisites.MeetsAsync(_prerequisiteEvaluators, userId, part, cancellationToken);

    private async Task<Enrollment> FindPreviousAsync(EnrollmentRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.PreviousEnrollmentId))
        {
            var named = await FindAsync(request.PreviousEnrollmentId, cancellationToken);

            if (named is not null && named.UserId == request.UserId)
            {
                return named;
            }
        }

        var enrollments = await _enrollmentStore.ListByCourseForUserAsync(request.UserId, request.CourseId, cancellationToken);

        return enrollments.Count > 0 ? enrollments[0] : null;
    }

    private async Task<Enrollment> FindAsync(string enrollmentId, CancellationToken cancellationToken)
        => string.IsNullOrEmpty(enrollmentId) ? null : await _enrollmentStore.FindByIdAsync(enrollmentId, cancellationToken);

    private void Move(Enrollment enrollment, EnrollmentStatus to, string actor, string reason)
    {
        var now = _clock.UtcNow;

        enrollment.History.Add(new EnrollmentStatusChange
        {
            From = enrollment.Status,
            To = to,
            ChangedUtc = now,
            Actor = actor,
            Reason = reason,
        });

        enrollment.Status = to;
        enrollment.ModifiedUtc = now;
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
}

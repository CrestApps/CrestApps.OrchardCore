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

        await using (locker)
        {
            var now = _clock.UtcNow;
            var open = await _enrollmentStore.FindOpenAsync(request.UserId, request.CourseId, offeringId, cancellationToken);

            if (open is not null)
            {
                return await AddGrantAsync(open, request, now, cancellationToken);
            }

            if (!request.IgnorePrerequisites && !await MeetsPrerequisitesAsync(request.UserId, course.Get<CoursePart>(nameof(CoursePart)), cancellationToken))
            {
                return EnrollmentResult.Fail(EnrollmentErrors.PrerequisitesNotMet);
            }

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

            if (enrollment.Status == EnrollmentStatus.Pending)
            {
                await NotifyAsync(observer => observer.RequestedAsync(enrollment, cancellationToken));
            }
            else
            {
                await NotifyAsync(observer => observer.EnrolledAsync(enrollment, cancellationToken));
            }

            return new EnrollmentResult
            {
                Enrollment = enrollment,
                Created = true,
                Changed = true,
            };
        }
    }

    /// <inheritdoc />
    public async Task<EnrollmentResult> ApproveAsync(string enrollmentId, string actor, CancellationToken cancellationToken = default)
    {
        var enrollment = await FindAsync(enrollmentId, cancellationToken);

        if (enrollment is null)
        {
            return EnrollmentResult.Fail(EnrollmentErrors.NotFound);
        }

        if (enrollment.Status != EnrollmentStatus.Pending)
        {
            return EnrollmentResult.Fail(EnrollmentErrors.InvalidTransition);
        }

        Move(enrollment, EnrollmentStatus.Active, actor, null);
        await _enrollmentStore.SaveAsync(enrollment, cancellationToken);
        await NotifyAsync(observer => observer.StatusChangedAsync(enrollment, EnrollmentStatus.Pending, cancellationToken));
        await NotifyAsync(observer => observer.EnrolledAsync(enrollment, cancellationToken));

        return new EnrollmentResult { Enrollment = enrollment, Changed = true };
    }

    /// <inheritdoc />
    public async Task<EnrollmentResult> RejectAsync(string enrollmentId, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var enrollment = await FindAsync(enrollmentId, cancellationToken);

        if (enrollment is null)
        {
            return EnrollmentResult.Fail(EnrollmentErrors.NotFound);
        }

        if (enrollment.Status != EnrollmentStatus.Pending)
        {
            return EnrollmentResult.Fail(EnrollmentErrors.InvalidTransition);
        }

        return await ChangeStatusAsync(enrollmentId, EnrollmentStatus.Withdrawn, actor, reason, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EnrollmentResult> ChangeStatusAsync(string enrollmentId, EnrollmentStatus status, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var enrollment = await FindAsync(enrollmentId, cancellationToken);

        if (enrollment is null)
        {
            return EnrollmentResult.Fail(EnrollmentErrors.NotFound);
        }

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
        await NotifyAsync(observer => observer.StatusChangedAsync(enrollment, previous, cancellationToken));

        if (status == EnrollmentStatus.Completed)
        {
            await NotifyAsync(observer => observer.CompletedAsync(enrollment, cancellationToken));
        }

        return new EnrollmentResult { Enrollment = enrollment, Changed = true };
    }

    /// <inheritdoc />
    public async Task<EnrollmentResult> ExtendAsync(string enrollmentId, DateTime? accessEndsUtc, DateTime? dueUtc, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var enrollment = await FindAsync(enrollmentId, cancellationToken);

        if (enrollment is null)
        {
            return EnrollmentResult.Fail(EnrollmentErrors.NotFound);
        }

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
            await NotifyAsync(observer => observer.StatusChangedAsync(enrollment, previous, cancellationToken));
        }

        await NotifyAsync(observer => observer.ExtendedAsync(enrollment, actor, reason.Trim(), cancellationToken));

        return new EnrollmentResult { Enrollment = enrollment, Changed = true };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Enrollment>> RevokeGrantAsync(string source, string sourceId, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var changed = new List<Enrollment>();

        foreach (var enrollment in await _enrollmentStore.ListByGrantAsync(source, sourceId, cancellationToken))
        {
            if (!enrollment.IsOpen && enrollment.Status != EnrollmentStatus.Expired)
            {
                continue;
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
                continue;
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
                await NotifyAsync(observer => observer.StatusChangedAsync(enrollment, previous, cancellationToken));
            }

            changed.Add(enrollment);
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

            foreach (var enrollment in page)
            {
                if (!seen.Add(enrollment.ItemId) || !EnrollmentTransitions.CanMove(enrollment.Status, EnrollmentStatus.Expired))
                {
                    continue;
                }

                var previous = enrollment.Status;

                Move(enrollment, EnrollmentStatus.Expired, SystemActor, "Access ended");
                await _enrollmentStore.SaveAsync(enrollment, cancellationToken);
                await NotifyAsync(observer => observer.StatusChangedAsync(enrollment, previous, cancellationToken));

                expired++;
                moved = true;
            }

            if (!moved || page.Count < PageSize)
            {
                break;
            }
        }

        return expired;
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

    private async Task<EnrollmentResult> AddGrantAsync(Enrollment open, EnrollmentRequest request, DateTime now, CancellationToken cancellationToken)
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
            await NotifyAsync(observer => observer.StatusChangedAsync(open, previous, cancellationToken));
            await NotifyAsync(observer => observer.EnrolledAsync(open, cancellationToken));
        }

        return new EnrollmentResult { Enrollment = open, Changed = true };
    }

    private async Task<bool> MeetsPrerequisitesAsync(string userId, CoursePart part, CancellationToken cancellationToken)
    {
        foreach (var prerequisite in part?.Prerequisites ?? [])
        {
            if (prerequisite is null || string.IsNullOrEmpty(prerequisite.Kind) || string.IsNullOrEmpty(prerequisite.Id))
            {
                continue;
            }

            var evaluator = _prerequisiteEvaluators.FirstOrDefault(candidate => string.Equals(candidate.Kind, prerequisite.Kind, StringComparison.OrdinalIgnoreCase));

            if (evaluator is null || !await evaluator.IsMetAsync(userId, prerequisite, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

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

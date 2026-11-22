using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Decides whether a person may open a course item. Course authors (<see cref="LearningPermissions.ManageCourses"/>) and
/// the course staff always may; anyone may open a free preview; a learner may open an item of a course they have an
/// active enrollment in, within its access dates, once the course has started and the item's release rules are met, and
/// may review a completed course while its access lasts.
/// </summary>
public sealed class DefaultCourseAccessService : ICourseAccessService
{
    private readonly ICourseStructureService _courseStructure;
    private readonly IEnrollmentStore _enrollmentStore;
    private readonly EnrollmentProgressStore _progressStore;
    private readonly CourseReleaseEvaluator _releaseEvaluator;
    private readonly Lazy<IAuthorizationService> _authorizationService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCourseAccessService"/> class.
    /// </summary>
    /// <param name="courseStructure">The course structure reader.</param>
    /// <param name="enrollmentStore">The enrollment store.</param>
    /// <param name="progressStore">The progress store.</param>
    /// <param name="releaseEvaluator">The release rule evaluator.</param>
    /// <param name="authorizationService">The authorization service, resolved lazily because authorization handlers use this service.</param>
    /// <param name="clock">The clock.</param>
    public DefaultCourseAccessService(
        ICourseStructureService courseStructure,
        IEnrollmentStore enrollmentStore,
        EnrollmentProgressStore progressStore,
        CourseReleaseEvaluator releaseEvaluator,
        Lazy<IAuthorizationService> authorizationService,
        IClock clock)
    {
        _courseStructure = courseStructure;
        _enrollmentStore = enrollmentStore;
        _progressStore = progressStore;
        _releaseEvaluator = releaseEvaluator;
        _authorizationService = authorizationService;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<CourseItemAccess> GetItemAccessAsync(ClaimsPrincipal user, ContentItem item)
    {
        var access = await DecideAsync(user, item);

        // A free preview opens for anyone, but a learner with access is still a learner there, so their progress counts.
        // It opens only when the item is in the outline of the published course.
        if (access.Reason is not (CourseItemAccessReason.NotACourseItem or CourseItemAccessReason.NotInCourse or CourseItemAccessReason.Staff or CourseItemAccessReason.Enrolled)
            && item.Get<CourseItemPart>(nameof(CourseItemPart))?.FreePreview == true
            && await IsInPublishedOutlineAsync(access.CourseId, item))
        {
            access.Reason = CourseItemAccessReason.FreePreview;
            access.LockedReason = null;
            access.AvailableFromUtc = null;
        }

        return access;
    }

    private async Task<CourseItemAccess> DecideAsync(ClaimsPrincipal user, ContentItem item)
    {
        var part = item?.Get<CourseItemPart>(nameof(CourseItemPart));

        if (part is null)
        {
            return new CourseItemAccess { Reason = CourseItemAccessReason.NotACourseItem };
        }

        var courseId = _courseStructure.GetCourseId(item);
        var access = new CourseItemAccess { CourseId = courseId };
        var authenticated = user?.Identity?.IsAuthenticated == true;
        var userId = LearningSurveyUsers.GetUserId(user);

        if (authenticated
            && (await _authorizationService.Value.AuthorizeAsync(user, LearningPermissions.ManageCourses)
                || (courseId is not null && await _courseStructure.IsCourseStaffAsync(userId, courseId) && await _authorizationService.Value.AuthorizeAsync(user, LearningPermissions.ManageOwnCourses))
                || await _authorizationService.Value.AuthorizeAsync(user, CommonPermissions.EditContent, item)))
        {
            access.Reason = CourseItemAccessReason.Staff;

            return access;
        }

        // A course item that is not in a course fails closed: it is not public because ordinary View content allows it.
        if (courseId is null)
        {
            access.Reason = CourseItemAccessReason.NotInCourse;

            return access;
        }

        if (string.IsNullOrEmpty(userId))
        {
            access.Reason = CourseItemAccessReason.Anonymous;

            return access;
        }

        var enrollments = await _enrollmentStore.ListByCourseForUserAsync(userId, courseId);
        var enrollment = enrollments.FirstOrDefault(candidate => candidate.IsOpen)
            ?? enrollments.FirstOrDefault(candidate => candidate.Status == EnrollmentStatus.Completed)
            ?? enrollments.FirstOrDefault(candidate => candidate.Status == EnrollmentStatus.Expired);

        access.Enrollment = enrollment;

        if (enrollment is null)
        {
            access.Reason = CourseItemAccessReason.NotEnrolled;

            return access;
        }

        var now = _clock.UtcNow;

        switch (enrollment.Status)
        {
            case EnrollmentStatus.Pending:
                access.Reason = CourseItemAccessReason.Pending;

                return access;

            case EnrollmentStatus.Suspended:
                access.Reason = CourseItemAccessReason.Suspended;

                return access;

            case EnrollmentStatus.Expired:
                access.Reason = CourseItemAccessReason.AccessEnded;

                return access;

            case EnrollmentStatus.Completed:
                if (enrollment.Grants.Count == 0 || (enrollment.AccessEndsUtc is not null && enrollment.AccessEndsUtc <= now))
                {
                    access.Reason = CourseItemAccessReason.AccessEnded;

                    return access;
                }

                break;

            default:
                if (!EnrollmentAccess.HasAccess(enrollment, now))
                {
                    access.Reason = CourseItemAccessReason.AccessEnded;

                    return access;
                }

                break;
        }

        var course = await _courseStructure.GetCourseAsync(courseId);
        var entry = course is null ? null : (await _courseStructure.GetOutlineAsync(course)).Find(item.ContentItemId);

        if (entry is null)
        {
            access.Reason = CourseItemAccessReason.NotReleased;
            access.LockedReason = LearningRefusals.NotInOutline;

            return access;
        }

        // Before the course starts, an enrolled learner sees it but opens none of its items. The end date never locks
        // the course, as in Moodle; the access length after enrollment still ends access above.
        var coursePart = course.Get<CoursePart>(nameof(CoursePart));

        if (enrollment.Status != EnrollmentStatus.Completed && CourseSchedules.Of(coursePart, now) == CourseSchedule.NotStarted)
        {
            access.Reason = CourseItemAccessReason.NotReleased;
            access.LockedReason = LearningRefusals.CourseNotStarted;
            access.AvailableFromUtc = coursePart.StartsUtc;

            return access;
        }

        // A learner who completed the course may review every item of its outline, whatever its release rules.
        if (enrollment.Status == EnrollmentStatus.Completed)
        {
            access.Reason = CourseItemAccessReason.Enrolled;

            return access;
        }

        var progress = await _progressStore.FindByEnrollmentAsync(enrollment.ItemId);
        var release = await _releaseEvaluator.EvaluateAsync(entry, enrollment, progress);

        if (!release.IsReleased)
        {
            access.Reason = CourseItemAccessReason.NotReleased;
            access.LockedReason = release.Reason;
            access.AvailableFromUtc = release.AvailableFromUtc;
            access.BlockingItemId = release.BlockingItemId;

            return access;
        }

        access.Reason = CourseItemAccessReason.Enrolled;

        return access;
    }

    private async Task<bool> IsInPublishedOutlineAsync(string courseId, ContentItem item)
    {
        if (string.IsNullOrEmpty(courseId))
        {
            return false;
        }

        var course = await _courseStructure.GetCourseAsync(courseId);

        return course is not null && (await _courseStructure.GetOutlineAsync(course)).Find(item.ContentItemId) is not null;
    }
}

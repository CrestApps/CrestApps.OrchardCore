using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Decides whether a person may open a course item. Course authors (<see cref="LearningPermissions.ManageCourses"/>) and
/// the course staff always may; anyone may open a free preview; a learner may open an item of a course they have an
/// active enrollment in, within its access dates, once its release rules are met, and may review a completed course
/// while its access lasts.
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
        var part = item?.Get<CourseItemPart>(nameof(CourseItemPart));
        var courseId = part is null ? null : _courseStructure.GetCourseId(item);

        if (courseId is null)
        {
            return new CourseItemAccess { Reason = CourseItemAccessReason.NotACourseItem };
        }

        var access = new CourseItemAccess { CourseId = courseId };
        var authenticated = user?.Identity?.IsAuthenticated == true;
        var userId = authenticated ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;

        if (authenticated)
        {
            if (await _authorizationService.Value.AuthorizeAsync(user, LearningPermissions.ManageCourses) || await _courseStructure.IsCourseStaffAsync(userId, courseId))
            {
                access.Reason = CourseItemAccessReason.Staff;

                return access;
            }
        }

        if (part.FreePreview)
        {
            access.Reason = CourseItemAccessReason.FreePreview;

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
                access.Reason = enrollment.Grants.Count > 0 && (enrollment.AccessEndsUtc is null || enrollment.AccessEndsUtc > now)
                    ? CourseItemAccessReason.Enrolled
                    : CourseItemAccessReason.AccessEnded;

                return access;
        }

        if (!EnrollmentAccess.HasAccess(enrollment, now))
        {
            access.Reason = CourseItemAccessReason.AccessEnded;

            return access;
        }

        var course = await _courseStructure.GetCourseAsync(courseId);
        var entry = course is null ? null : (await _courseStructure.GetOutlineAsync(course)).Find(item.ContentItemId);

        if (entry is null)
        {
            access.Reason = CourseItemAccessReason.NotReleased;
            access.LockedReason = "This item is not part of the course outline.";

            return access;
        }

        var progress = await _progressStore.FindByEnrollmentAsync(enrollment.ItemId);
        var release = await _releaseEvaluator.EvaluateAsync(entry, enrollment, progress);

        if (!release.IsReleased)
        {
            access.Reason = CourseItemAccessReason.NotReleased;
            access.LockedReason = release.Reason;
            access.AvailableFromUtc = release.AvailableFromUtc;

            return access;
        }

        access.Reason = CourseItemAccessReason.Enrolled;

        return access;
    }
}

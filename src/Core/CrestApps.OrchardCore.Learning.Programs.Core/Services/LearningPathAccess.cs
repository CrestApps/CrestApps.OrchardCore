using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Programs.Core.Services;

/// <summary>
/// The course enrollment controls a learning path must respect: staff who start people on a path or list who is on it
/// must be allowed to manage the enrollments of every course in it, and a person may start a path on their own only
/// when every course in it is open to enroll in.
/// </summary>
public static class LearningPathAccess
{
    /// <summary>
    /// Tells whether a user may manage the enrollments of every course in a path.
    /// </summary>
    public static async Task<bool> CanManageEnrollmentsAsync(IAuthorizationService authorizationService, ClaimsPrincipal user, LearningPathPart part)
    {
        ArgumentNullException.ThrowIfNull(part);

        foreach (var courseId in LearningPathRules.CourseIds(part))
        {
            if (!await authorizationService.AuthorizeAsync(user, LearningPermissions.ManageEnrollments, new LearningCourseResource(courseId)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Tells whether every course in a path is published and open to enroll in, so starting the path on one's own
    /// enters no course that needs a key or an approval.
    /// </summary>
    public static async Task<bool> AllCoursesOpenAsync(IContentManager contentManager, LearningPathPart part)
    {
        ArgumentNullException.ThrowIfNull(part);

        var ids = LearningPathRules.CourseIds(part).ToArray();

        if (ids.Length == 0)
        {
            return false;
        }

        var courses = (await contentManager.GetAsync(ids, VersionOptions.Published)).ToArray();

        return courses.Length == ids.Distinct(StringComparer.Ordinal).Count()
            && courses.All(course => course.TryGet<CoursePart>(out var coursePart) && coursePart.EnrollmentMode == EnrollmentMode.Open);
    }
}

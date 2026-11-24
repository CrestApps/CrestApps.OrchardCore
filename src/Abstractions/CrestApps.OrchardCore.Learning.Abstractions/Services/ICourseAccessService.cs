using System.Security.Claims;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Decides whether a person may open a course item: the course staff and authors always, anyone for a free preview, and
/// a learner with an active enrollment within its access dates once the release rules are met. The content access
/// handler on Orchard's <c>ViewContent</c>, and so Secure Media, apply this decision.
/// </summary>
public interface ICourseAccessService
{
    /// <summary>
    /// Decides whether a person may open a course item.
    /// </summary>
    /// <param name="user">The person, or an anonymous principal.</param>
    /// <param name="item">The course item.</param>
    /// <returns>The decision.</returns>
    Task<CourseItemAccess> GetItemAccessAsync(ClaimsPrincipal user, ContentItem item);
}

using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Reads courses and their outlines.
/// </summary>
public interface ICourseStructureService
{
    /// <summary>
    /// Gets a published course.
    /// </summary>
    /// <param name="courseId">The content item id of the course.</param>
    /// <returns>The course, or <see langword="null"/> when it does not exist, is not published or is not a course.</returns>
    Task<ContentItem> GetCourseAsync(string courseId);

    /// <summary>
    /// Gets the id of the course a course item belongs to, from Orchard's contained part.
    /// </summary>
    /// <param name="item">The course item.</param>
    /// <returns>The content item id of the course, or <see langword="null"/>.</returns>
    string GetCourseId(ContentItem item);

    /// <summary>
    /// Resolves the outline of a course: its sections with their published course items, in order.
    /// </summary>
    /// <param name="course">The course.</param>
    /// <returns>The outline.</returns>
    Task<CourseOutline> GetOutlineAsync(ContentItem course);

    /// <summary>
    /// Lists the courses where a user is on the course staff, optionally in some roles only.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="roles">The roles, or none for any role.</param>
    /// <returns>The content item ids of the courses.</returns>
    Task<IReadOnlyCollection<string>> GetStaffCourseIdsAsync(string userId, params string[] roles);

    /// <summary>
    /// Tells whether a user is on the staff of a course, optionally in some roles only.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="courseId">The content item id of the course.</param>
    /// <param name="roles">The roles, or none for any role.</param>
    /// <returns><see langword="true"/> when the user is on the staff.</returns>
    Task<bool> IsCourseStaffAsync(string userId, string courseId, params string[] roles);
}

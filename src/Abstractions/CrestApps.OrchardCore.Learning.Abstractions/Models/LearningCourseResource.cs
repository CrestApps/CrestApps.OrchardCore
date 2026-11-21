namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// The resource of a Learning permission check about a course rather than one enrollment, such as enrolling a learner
/// into it: <c>AuthorizeAsync(user, LearningPermissions.ManageEnrollments, new LearningCourseResource(courseId))</c>.
/// </summary>
/// <param name="CourseId">The content item id of the course.</param>
public sealed record LearningCourseResource(string CourseId);

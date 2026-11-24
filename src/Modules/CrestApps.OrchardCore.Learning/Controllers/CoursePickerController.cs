using CrestApps.OrchardCore.Learning.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Learning.Controllers;

/// <summary>
/// What the admin's course pickers search (course prerequisites, the courses of a learning path set, the courses of a
/// workflow event): the latest version of each course, by title, a page of them at a time, so a site with hundreds of
/// courses never renders them all. Only people who work with courses may search, and only titles leave.
/// </summary>
[Admin]
public sealed class CoursePickerController : Controller
{
    /// <summary>
    /// The route name of the search, for the ItemSelector component's endpoint.
    /// </summary>
    public const string SearchRouteName = "LearningCourseSearch";

    private const int MaxResults = 50;

    private readonly CourseLookup _courseLookup;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoursePickerController"/> class.
    /// </summary>
    /// <param name="courseLookup">The course lookup.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public CoursePickerController(CourseLookup courseLookup, IAuthorizationService authorizationService)
    {
        _courseLookup = courseLookup;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Returns the courses whose title contains the text, as <c>[{ value, text }]</c> for the ItemSelector component.
    /// </summary>
    /// <param name="query">The text to look for; empty for the first courses by title.</param>
    /// <param name="exceptId">A course to leave out, such as the one whose prerequisites are being chosen.</param>
    /// <returns>The courses found.</returns>
    [HttpGet]
    [Admin("learning/course-picker/search", SearchRouteName)]
    public async Task<IActionResult> Search(string query, string exceptId)
    {
        if (!await CanPickAsync())
        {
            return Forbid();
        }

        // One more than a page, so leaving out the course being edited still returns a full page.
        var courses = await _courseLookup.SearchCoursesAsync(query, MaxResults + 1, HttpContext.RequestAborted);

        return Ok(courses
            .Where(course => !string.Equals(course.ContentItemId, exceptId, StringComparison.Ordinal))
            .Take(MaxResults)
            .Select(course => new
            {
                value = course.ContentItemId,
                text = course.DisplayText,
            }));
    }

    private async Task<bool> CanPickAsync()
        => await _authorizationService.AuthorizeAsync(User, LearningPermissions.ManageOwnCourses)
        || await _authorizationService.AuthorizeAsync(User, LearningPermissions.ViewEnrollments)
        || await _authorizationService.AuthorizeAsync(User, LearningPermissions.ViewLearningReports);
}

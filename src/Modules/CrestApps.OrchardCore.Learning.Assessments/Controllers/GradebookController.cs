using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Assessments.Controllers;

/// <summary>
/// The gradebook of a course: every learner and the score of every graded item, the score of each gradebook category
/// with the items it dropped, and the course grade on a grade scale.
/// </summary>
[Admin]
[Feature(AssessmentsConstants.Features.Gradebook)]
public sealed class GradebookController : Controller
{
    private const int MaxRows = 500;

    private readonly EnrollmentStore _enrollmentStore;
    private readonly EnrollmentProgressStore _progressStore;
    private readonly ICourseStructureService _courseStructure;
    private readonly GradeScaleStore _scaleStore;
    private readonly IUserSearchService _userSearchService;
    private readonly IAuthorizationService _authorizationService;
    private readonly global::YesSql.ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="GradebookController"/> class.
    /// </summary>
    public GradebookController(
        EnrollmentStore enrollmentStore,
        EnrollmentProgressStore progressStore,
        ICourseStructureService courseStructure,
        GradeScaleStore scaleStore,
        IUserSearchService userSearchService,
        IAuthorizationService authorizationService,
        global::YesSql.ISession session)
    {
        _enrollmentStore = enrollmentStore;
        _progressStore = progressStore;
        _courseStructure = courseStructure;
        _scaleStore = scaleStore;
        _userSearchService = userSearchService;
        _authorizationService = authorizationService;
        _session = session;
    }

    /// <summary>
    /// Shows the gradebook of a course.
    /// </summary>
    /// <param name="model">The course and scale chosen.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("learning/gradebook", "LearningGradebook")]
    public async Task<IActionResult> Index(GradebookViewModel model)
    {
        var all = await _authorizationService.AuthorizeAsync(User, LearningPermissions.ViewLearnerRecords);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var staffCourses = all ? null : await _courseStructure.GetStaffCourseIdsAsync(userId);

        if (!all && staffCourses.Count == 0)
        {
            return Forbid();
        }

        var courses = await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType == LearningConstants.ContentTypes.Course)
            .OrderBy(index => index.DisplayText)
            .ListAsync();

        model.CourseOptions = courses.Where(course => all || staffCourses.Contains(course.ContentItemId))
            .Select(course => new SelectListItem(course.DisplayText, course.ContentItemId, course.ContentItemId == model.CourseId))
            .ToList();

        var scales = (await _scaleStore.GetAllAsync()).OrderBy(scale => scale.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        model.ScaleOptions = scales.Select(scale => new SelectListItem(scale.Name, scale.ItemId, scale.ItemId == model.ScaleId)).ToList();
        model.Scale = scales.FirstOrDefault(scale => scale.ItemId == model.ScaleId);

        if (string.IsNullOrEmpty(model.CourseId) || model.CourseOptions.All(option => option.Value != model.CourseId))
        {
            model.CourseId = null;

            return View(model);
        }

        var course = await _courseStructure.GetCourseAsync(model.CourseId);

        if (course is null)
        {
            return NotFound();
        }

        var outline = await _courseStructure.GetOutlineAsync(course);

        model.CourseTitle = course.DisplayText;
        model.Items = outline.Entries
            .Where(entry => entry.Part?.Gradable == true)
            .Select(entry => (entry.Item.ContentItemId, entry.Item.DisplayText, entry.Part.Weight))
            .ToList();

        // The category columns: a breakdown without progress lists every category, and the items without one last.
        model.Categories = CourseGradeCalculator.ComputeBreakdown(outline, null).Categories;

        var (count, enrollments) = await _enrollmentStore.PageAsync(new EnrollmentQuery { CourseId = course.ContentItemId }, 1, MaxRows);
        var progress = (await _progressStore.ListByEnrollmentsAsync(enrollments.Select(enrollment => enrollment.ItemId).ToArray()))
            .ToDictionary(entry => entry.EnrollmentId, StringComparer.Ordinal);
        var names = (await _userSearchService.GetAsync(enrollments.Select(enrollment => enrollment.UserId).Distinct(StringComparer.Ordinal)))
            .ToDictionary(user => user.UserId, user => string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName, StringComparer.Ordinal);

        model.Truncated = count > enrollments.Count;
        model.Rows = enrollments.Select(enrollment =>
        {
            var row = new GradebookRowViewModel
            {
                Enrollment = enrollment,
                LearnerName = names.TryGetValue(enrollment.UserId, out var name) ? name : enrollment.UserId,
            };

            if (progress.TryGetValue(enrollment.ItemId, out var entry))
            {
                foreach (var item in model.Items)
                {
                    if (entry.Find(item.Id) is { } itemProgress)
                    {
                        row.Items[item.Id] = itemProgress;
                    }
                }
            }

            row.Breakdown = CourseGradeCalculator.ComputeBreakdown(outline, entry);

            return row;
        }).OrderBy(row => row.LearnerName, StringComparer.CurrentCultureIgnoreCase).ToList();

        return View(model);
    }
}

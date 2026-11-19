using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Drivers;

/// <summary>
/// The learner's block on a course page: where the visitor stands, the button that enrolls or continues, and the
/// outline with the learner's states. The course page itself is the course content item's own display.
/// </summary>
internal sealed class CourseLearnerDisplayDriver : ContentPartDisplayDriver<CoursePart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICourseStructureService _courseStructure;
    private readonly IEnrollmentStore _enrollmentStore;
    private readonly ILearnerProgressService _progressService;
    private readonly IEnumerable<SelfEnrollmentMarker> _selfEnrollment;
    private readonly CourseItemKindOptions _kindOptions;
    private readonly CourseCategoryService _categories;
    private readonly IClock _clock;

    public CourseLearnerDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ICourseStructureService courseStructure,
        IEnrollmentStore enrollmentStore,
        ILearnerProgressService progressService,
        IEnumerable<SelfEnrollmentMarker> selfEnrollment,
        IOptions<CourseItemKindOptions> kindOptions,
        CourseCategoryService categories,
        IClock clock)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _courseStructure = courseStructure;
        _enrollmentStore = enrollmentStore;
        _progressService = progressService;
        _selfEnrollment = selfEnrollment;
        _kindOptions = kindOptions.Value;
        _categories = categories;
        _clock = clock;
    }

    public override IDisplayResult Display(CoursePart part, BuildPartDisplayContext context)
    {
        return Combine(
            Initialize<CourseLearnerViewModel>("CoursePart_Learner", model => PopulateAsync(model, part)).Location("Detail", "Content:20"),
            Initialize<CourseLearnerViewModel>("CoursePart_Summary", model =>
            {
                model.Part = part;
                model.CourseId = part.ContentItem.ContentItemId;
                model.Schedule = CourseSchedules.Of(part, _clock.UtcNow);
            }).Location("Summary", "Meta:5"));
    }

    private async ValueTask PopulateAsync(CourseLearnerViewModel model, CoursePart part)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var courseId = part.ContentItem.ContentItemId;

        model.Part = part;
        model.CourseId = courseId;
        model.SignedIn = user?.Identity?.IsAuthenticated == true;
        model.CanSelfEnroll = _selfEnrollment.Any() && part.EnrollmentMode != EnrollmentMode.Closed;
        model.KindIcons = _kindOptions.Kinds.ToDictionary(kind => kind.Name, kind => kind.Icon, StringComparer.OrdinalIgnoreCase);
        model.Outline = await _courseStructure.GetOutlineAsync(part.ContentItem);
        model.Schedule = CourseSchedules.Of(part, _clock.UtcNow);

        var tree = await _categories.GetTreeAsync();

        foreach (var categoryId in await _categories.GetCategoryIdsAsync(part.ContentItem))
        {
            if (tree.GetPathText(categoryId) is { } path)
            {
                model.Categories.Add(path);
            }
        }

        if (!model.SignedIn)
        {
            return;
        }

        model.IsStaff = await _authorizationService.AuthorizeAsync(user, LearningPermissions.ManageCourses) || await _courseStructure.IsCourseStaffAsync(userId, courseId);

        var enrollments = await _enrollmentStore.ListByCourseForUserAsync(userId, courseId);

        model.Enrollment = enrollments.FirstOrDefault(enrollment => enrollment.IsOpen) ?? enrollments.FirstOrDefault(enrollment => enrollment.Status == EnrollmentStatus.Completed);

        if (model.Enrollment is not null)
        {
            model.Progress = await _progressService.GetAsync(model.Enrollment.ItemId);
        }
    }
}

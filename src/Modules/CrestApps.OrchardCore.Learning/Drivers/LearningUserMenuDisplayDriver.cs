using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Learning.Drivers;

/// <summary>
/// Adds My courses to the user menu, on the site's pages and in the admin, beside Profile and Security: a link to My
/// learning followed by the learner's active courses, each opening the course player, and See all when there are more.
/// The courses come from one capped, indexed query of the learner's active enrollments and one read of their courses,
/// and only for people with Access learning.
/// </summary>
internal sealed class LearningUserMenuDisplayDriver : DisplayDriver<UserMenu>
{
    /// <summary>
    /// The most courses the user menu lists.
    /// </summary>
    internal const int MaxCourses = 5;

    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly EnrollmentStore _enrollmentStore;
    private readonly IContentManager _contentManager;

    public LearningUserMenuDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        EnrollmentStore enrollmentStore,
        IContentManager contentManager)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _enrollmentStore = enrollmentStore;
        _contentManager = contentManager;
    }

    public override async Task<IDisplayResult> DisplayAsync(UserMenu model, BuildDisplayContext context)
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated != true ||
            !await _authorizationService.AuthorizeAsync(principal, LearningPermissions.AccessLearning))
        {
            return null;
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Initialize<LearningUserMenuViewModel>("UserMenuItems__LearningCourses", async viewModel =>
            {
                // One more than shown tells whether See all is needed.
                var enrollments = await _enrollmentStore.ListActiveForUserAsync(userId, MaxCourses + 1);
                var courseIds = enrollments.Select(enrollment => enrollment.CourseId).Distinct(StringComparer.Ordinal).ToArray();
                var courses = courseIds.Length == 0
                    ? new Dictionary<string, ContentItem>(StringComparer.Ordinal)
                    : (await _contentManager.GetAsync(courseIds, VersionOptions.Published)).ToDictionary(course => course.ContentItemId, StringComparer.Ordinal);

                viewModel.Courses = courseIds
                    .Where(courses.ContainsKey)
                    .Take(MaxCourses)
                    .Select(courseId => new LearningUserMenuCourse
                    {
                        CourseId = courseId,
                        Title = courses[courseId].DisplayText,
                    })
                    .ToList();
                viewModel.HasMore = courseIds.Length > MaxCourses;
            })
            .Location(OrchardCoreConstants.DisplayType.Detail, "Content:3")
            .Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Content:3")
            .Differentiator("LearningCourses");
    }
}

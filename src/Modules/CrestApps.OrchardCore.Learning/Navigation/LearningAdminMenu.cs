using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Navigation;

/// <summary>
/// The Learning admin menu: courses (Orchard's content list of courses) and enrollments. Course staff, who hold Manage
/// own courses, see Enrollments too: the page lists the enrollments of their own courses.
/// </summary>
public sealed class LearningAdminMenu : AdminNavigationProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningAdminMenu"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearningAdminMenu(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<LearningAdminMenu> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        // A menu item needs every permission it lists, so the item asks for the one the person holds: View enrollments
        // for every enrollment, otherwise Manage own courses for the enrollments of their own courses.
        var enrollmentsPermission = await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, LearningPermissions.ViewEnrollments)
            ? LearningPermissions.ViewEnrollments
            : LearningPermissions.ManageOwnCourses;

        builder.Add(S["Learning"], "after", learning => learning
            .Id("learning")
            .AddClass("learning")
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-graduation-cap")
            .Add(S["Courses"], S["Courses"].PrefixPosition(), courses => courses
                .Id("learningCourses")
                .Action("List", "Admin", new { area = "OrchardCore.Contents", contentTypeId = LearningConstants.ContentTypes.Course })
                .Permission(LearningPermissions.ManageOwnCourses)
                .LocalNav())
            .Add(S["Enrollments"], S["Enrollments"].PrefixPosition(), enrollments => enrollments
                .Id("learningEnrollments")
                .Action("Index", "Enrollments", LearningConstants.Features.Learning)
                .Permission(enrollmentsPermission)
                .LocalNav()), priority: 1);
    }
}

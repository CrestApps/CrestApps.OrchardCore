using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Assessments.Navigation;

/// <summary>
/// Adds the gradebook and grade scales to the Learning admin menu.
/// </summary>
public sealed class GradebookAdminMenu : AdminNavigationProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="GradebookAdminMenu"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public GradebookAdminMenu(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<GradebookAdminMenu> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        // A menu item needs every permission it lists, so Gradebook asks for the one the person holds: View learner records for every course, otherwise Manage own courses for their own courses.
        var gradebookPermission = await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, LearningPermissions.ViewLearnerRecords)
            ? LearningPermissions.ViewLearnerRecords
            : LearningPermissions.ManageOwnCourses;

        builder.Add(S["Learning"], learning => learning
            .Add(S["Gradebook"], S["Gradebook"].PrefixPosition(), gradebook => gradebook
                .Id("learningGradebook")
                .Action("Index", "Gradebook", AssessmentsConstants.Features.Assessments)
                .Permission(gradebookPermission)
                .LocalNav())
            .Add(S["Grade scales"], S["Grade scales"].PrefixPosition(), scales => scales
                .Id("learningGradeScales")
                .Action("Index", "GradeScales", AssessmentsConstants.Features.Assessments)
                .Permission(AssessmentPermissions.ManageGradeScales)
                .LocalNav()));
    }
}

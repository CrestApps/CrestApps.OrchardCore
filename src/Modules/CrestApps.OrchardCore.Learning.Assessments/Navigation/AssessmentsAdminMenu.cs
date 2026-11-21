using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Assessments.Navigation;

/// <summary>
/// Adds Question banks and Grading to the Learning admin menu.
/// </summary>
public sealed class AssessmentsAdminMenu : AdminNavigationProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssessmentsAdminMenu"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AssessmentsAdminMenu(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<AssessmentsAdminMenu> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        // A menu item needs every permission it lists, so Grading asks for the one the person holds: Grade assessments for every course, otherwise Manage own courses for the courses where they grade.
        var gradingPermission = await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, AssessmentPermissions.GradeAssessments)
            ? AssessmentPermissions.GradeAssessments
            : LearningPermissions.ManageOwnCourses;

        builder.Add(S["Learning"], learning => learning
            .Add(S["Question banks"], S["Question banks"].PrefixPosition(), banks => banks
                .Id("learningQuestionBanks")
                .Action("List", "Admin", new { area = "OrchardCore.Contents", contentTypeId = AssessmentsConstants.ContentTypes.QuestionBank })
                .Permission(AssessmentPermissions.ManageQuestionBanks)
                .LocalNav())
            .Add(S["Grading"], S["Grading"].PrefixPosition(), grading => grading
                .Id("learningGrading")
                .Action("Index", "Grading", AssessmentsConstants.Features.Assessments)
                .Permission(gradingPermission)
                .LocalNav()));
    }
}

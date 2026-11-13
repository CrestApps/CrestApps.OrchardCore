using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Assessments.Navigation;

/// <summary>
/// Adds Question banks and Grading to the Learning admin menu.
/// </summary>
public sealed class AssessmentsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssessmentsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AssessmentsAdminMenu(IStringLocalizer<AssessmentsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Learning"], learning => learning
            .Add(S["Question banks"], S["Question banks"].PrefixPosition(), banks => banks
                .Id("learningQuestionBanks")
                .Action("List", "Admin", new { area = "OrchardCore.Contents", contentTypeId = AssessmentsConstants.ContentTypes.QuestionBank })
                .Permission(AssessmentPermissions.ManageQuestionBanks)
                .LocalNav())
            .Add(S["Grading"], S["Grading"].PrefixPosition(), grading => grading
                .Id("learningGrading")
                .Action("Index", "Grading", AssessmentsConstants.Features.Assessments)
                .Permission(AssessmentPermissions.GradeAssessments)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

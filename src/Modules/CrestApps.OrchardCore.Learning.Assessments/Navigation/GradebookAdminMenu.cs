using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Assessments.Navigation;

/// <summary>
/// Adds the gradebook and grade scales to the Learning admin menu.
/// </summary>
public sealed class GradebookAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="GradebookAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public GradebookAdminMenu(IStringLocalizer<GradebookAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Learning"], learning => learning
            .Add(S["Gradebook"], S["Gradebook"].PrefixPosition(), gradebook => gradebook
                .Id("learningGradebook")
                .Action("Index", "Gradebook", AssessmentsConstants.Features.Assessments)
                .Permission(LearningPermissions.ViewLearnerRecords)
                .LocalNav())
            .Add(S["Grade scales"], S["Grade scales"].PrefixPosition(), scales => scales
                .Id("learningGradeScales")
                .Action("Index", "GradeScales", AssessmentsConstants.Features.Assessments)
                .Permission(AssessmentPermissions.ManageGradeScales)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Navigation;

/// <summary>
/// The Learning admin menu: courses (Orchard's content list of courses) and enrollments.
/// </summary>
public sealed class LearningAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearningAdminMenu(IStringLocalizer<LearningAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
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
                .Permission(LearningPermissions.ViewEnrollments)
                .LocalNav()), priority: 1);

        return ValueTask.CompletedTask;
    }
}

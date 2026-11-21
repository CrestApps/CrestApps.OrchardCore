using CrestApps.OrchardCore.Learning.Programs.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Programs.Services;

/// <summary>
/// Adds Required learning to the Learning menu.
/// </summary>
internal sealed class ProgramsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public ProgramsAdminMenu(IStringLocalizer<ProgramsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Learning"], learning => learning
            .Add(S["Required Learning"], S["Required Learning"].PrefixPosition(), required => required
                .AddClass("required-learning")
                .Id("requiredLearning")
                .Action("Index", "RequiredLearning", ProgramsConstants.Features.Paths)
                .Permission(LearningProgramsPermissions.ManageRequiredLearning)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Adds Team learning to the Learning menu.
/// </summary>
internal sealed class TeamLearningAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public TeamLearningAdminMenu(IStringLocalizer<TeamLearningAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Learning"], learning => learning
            .Add(S["Team Learning"], S["Team Learning"].PrefixPosition(), team => team
                .AddClass("team-learning")
                .Id("teamLearning")
                .Action("Index", "TeamLearning", ProgramsConstants.Features.Paths)
                .Permission(LearningProgramsPermissions.ViewTeamLearning)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Navigation;

/// <summary>
/// Adds the Workers, Teams and Activity Codes items to the Workforce menu.
/// </summary>
public sealed class WorkforceAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceAdminMenu(IStringLocalizer<WorkforceAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Workforce"], S["Workforce"].PrefixPosition("6"), workforce => workforce
            .AddClass("workforce")
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-users")
            .Id("workforce")
            .Add(S["Get Started"], S["Get Started"].PrefixPosition("01"), start => start
                .Id("workforceGetStarted")
                .Action("Index", "WorkforceSetup", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageTeams)
                .LocalNav())
            .Add(S["Workers"], S["Workers"].PrefixPosition("10"), workers => workers
                .Id("workers")
                .Action("Index", "Workers", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ViewWorkers)
                .LocalNav())
            .Add(S["Teams"], S["Teams"].PrefixPosition("11"), teams => teams
                .Id("workforceTeams")
                .Action("Index", "Teams", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageTeams)
                .LocalNav())
            .Add(S["Activity Codes"], S["Activity Codes"].PrefixPosition("90"), codes => codes
                .Id("activityCodes")
                .Action("Index", "ActivityCodes", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageActivityCodes)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

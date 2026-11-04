using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Teams.Navigation;

/// <summary>
/// Adds Teams to the Content admin menu.
/// </summary>
public sealed class TeamsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TeamsAdminMenu(IStringLocalizer<TeamsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        // Teams are shared by Workforce, Contact Center and other modules, so they sit under Content rather than any one of them.
        builder.Add(S["Content"], content => content
            .Add(S["Teams"], S["Teams"].PrefixPosition(), teams => teams
                .Id("teams")
                .Action("Index", "Teams", TeamsConstants.Features.Teams)
                .Permission(TeamsPermissions.ManageTeams)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

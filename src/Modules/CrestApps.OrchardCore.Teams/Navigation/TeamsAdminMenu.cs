using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Teams.Navigation;

/// <summary>
/// Adds the top-level Teams item to the admin menu.
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
        builder.Add(S["Teams"], S["Teams"].PrefixPosition("6.5"), teams => teams
            .AddClass("teams")
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-people-group")
            .Id("teams")
            .Action("Index", "Teams", TeamsConstants.Features.Teams)
            .Permission(TeamsPermissions.ManageTeams)
            .LocalNav());

        return ValueTask.CompletedTask;
    }
}

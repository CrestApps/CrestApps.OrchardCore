using CrestApps.OrchardCore.Users.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Users.Cohorts;

/// <summary>
/// Adds Cohorts under Access Control, beside User Audiences.
/// </summary>
public sealed class CohortsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CohortsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CohortsAdminMenu(IStringLocalizer<CohortsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Access Control"], NavigationConstants.AdminMenuAccessControlPosition, accessControl => accessControl
                .Add(S["Cohorts"], S["Cohorts"].PrefixPosition(), cohorts => cohorts
                    .AddClass("cohorts")
                    .Id("cohorts")
                    .Action("Index", "Cohorts", UsersConstants.Feature.Area)
                    .Permission(UserPermissions.ManageCohorts)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

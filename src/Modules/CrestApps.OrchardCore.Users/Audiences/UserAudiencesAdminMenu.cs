using CrestApps.OrchardCore.Users.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Users.Audiences;

/// <summary>
/// Adds User Audiences under Content, beside Teams and Cohorts: audiences pick the people other modules target, and grant
/// no permission, so they are not access control.
/// </summary>
public sealed class UserAudiencesAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAudiencesAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UserAudiencesAdminMenu(IStringLocalizer<UserAudiencesAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Content"], content => content
                .Add(S["User Audiences"], S["User Audiences"].PrefixPosition(), audiences => audiences
                    .AddClass("user-audiences")
                    .Id("userAudiences")
                    .Action("Index", "UserAudiences", UsersConstants.Feature.Area)
                    .Permission(UserPermissions.ManageUserAudiences)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

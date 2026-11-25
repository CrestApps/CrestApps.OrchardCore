using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, read, update, enable, disable, unlock and delete user accounts and confirm an
/// email, when the Users feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Users)]
public sealed class UsersToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UsersToolsStartup(IStringLocalizer<UsersToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListUsersTool>(ListUsersTool.TheName)
            .WithTitle(S["List Users"])
            .WithDescription(S["Lists user accounts with optional filters (text in the user name, email, role, enabled or disabled), sorting and paging. Use it to find a user's id before any other user tool; it returns id, user name, email, roles and status only, never password or token data."])
            .WithCategory(S["Users Management"])
            .Selectable();

        services.AddCoreAITool<GetUserTool>(GetUserTool.TheName)
            .WithTitle(S["Get User"])
            .WithDescription(S["Gets one user by id, user name or email: profile, roles, enabled, email confirmed, whether a password is set, locked out until, failed sign-in count, two-factor state, external login provider names, time zone, culture and custom user settings. Call it before changing a user."])
            .WithCategory(S["Users Management"])
            .Selectable();

        services.AddCoreAITool<SaveUserTool>(SaveUserTool.TheName)
            .WithTitle(S["Save User"])
            .WithDescription(S["Updates a user's user name, email and phone number. It cannot create a user and it cannot set a password; roles are changed with setUserRoles."])
            .WithCategory(S["Users Management"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<SetUserAccountStateTool>(SetUserAccountStateTool.TheName)
            .WithTitle(S["Set User Account State"])
            .WithDescription(S["Enables, disables or unlocks one or more user accounts, or marks their email as confirmed without sending an email. Disabling refuses the calling user's own account, the site super user and the only enabled administrator. Use enable to approve users waiting for moderation and unlock when getUser shows a lockout."])
            .WithCategory(S["Users Management"])
            .Selectable();

        services.AddCoreAITool<DeleteUserTool>(DeleteUserTool.TheName)
            .WithTitle(S["Delete User"])
            .WithDescription(S["Permanently deletes a user account; this cannot be undone. It refuses for the calling user's own account, the site super user and the only enabled administrator; suggest setUserAccountState with action disable when the admin only wants to block access."])
            .WithCategory(S["Users Management"])
            .AsDestructive()
            .Selectable();
    }
}

using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that gives roles to a user and takes roles from a user, and the tool that explains why a user can
/// or cannot do something, when the Users and Roles features are on. Orchard Core adds the role editor of a user, the
/// permission to assign a role and the role permissions of a signed-in user under the same condition.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Users, OrchardCoreFeatureIds.Roles)]
public sealed class UserRolesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRolesToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UserRolesToolsStartup(IStringLocalizer<UserRolesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SetUserRolesTool>(SetUserRolesTool.TheName)
            .WithTitle(S["Set User Roles"])
            .WithDescription(S["Adds roles to a user or removes roles from a user. It refuses to change the calling user's own roles, to remove the administrator role from the only enabled administrator, and to touch a role the caller may not assign."])
            .WithCategory(S["Users Management"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ExplainUserAccessTool>(ExplainUserAccessTool.TheName)
            .WithTitle(S["Explain User Access"])
            .WithDescription(S["Explains why a user can or cannot do something, for questions like \"why can't Jane edit this page?\": it asks the site's own authorization for the user and a permission, optionally on a content item, and returns the answer, the user's roles that hold the permission, the roles of the site that would grant it, and whether the account is disabled or locked out. Use listPermissions for permission names."])
            .WithCategory(S["Users Management"])
            .Selectable();
    }
}

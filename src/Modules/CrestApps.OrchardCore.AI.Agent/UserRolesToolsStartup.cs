using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that gives roles to a user and takes roles from a user, when the Users and Roles features are
/// on. Orchard Core adds the role editor of a user, and the permission to assign a role, under the same condition.
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
    }
}

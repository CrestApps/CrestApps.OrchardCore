using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Roles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list and read roles and permissions and that save and delete a role, when the Roles
/// feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Roles)]
public sealed class RolesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RolesToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RolesToolsStartup(IStringLocalizer<RolesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListRolesTool>(ListRolesTool.TheName)
            .WithTitle(S["List Roles"])
            .WithDescription(S["Lists all roles with their description and whether each is a system role or the administrator role. Use it to get exact role names."])
            .WithCategory(S["Roles Management"])
            .Selectable();

        services.AddCoreAITool<GetRoleDetailsTool>(GetRoleDetailsTool.TheName)
            .WithTitle(S["Get Role"])
            .WithDescription(S["Gets a role's description and permissions: the ones assigned directly and the effective ones, including those inherited from Anonymous and Authenticated. With compareWith, it compares the permissions of two roles. Use it to answer \"what can this role do\" or \"how do these roles differ\" and before saveRole."])
            .WithCategory(S["Roles Management"])
            .Selectable();

        services.AddCoreAITool<ListPermissionsTool>(ListPermissionsTool.TheName)
            .WithTitle(S["List Permissions"])
            .WithDescription(S["Lists the permissions that exist on this site, grouped by feature, with name, description and whether each is security critical; optional text filter. Use it to find exact permission names before saveRole."])
            .WithCategory(S["Roles Management"])
            .Selectable();

        services.AddCoreAITool<SaveRoleTool>(SaveRoleTool.TheName)
            .WithTitle(S["Save Role"])
            .WithDescription(S["Creates a role, or updates a role's description and permissions (add, remove or replace; a new role can copy the permissions of another role). It refuses to change the administrator role's permissions and names every security-critical permission it would grant. A permission name no enabled feature provides is refused with the closest names that exist."])
            .WithCategory(S["Roles Management"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<DeleteRoleTool>(DeleteRoleTool.TheName)
            .WithTitle(S["Delete Role"])
            .WithDescription(S["Deletes a custom role and removes it from every user and OpenID application that has it; this cannot be undone. System roles cannot be deleted."])
            .WithCategory(S["Roles Management"])
            .AsSecurity()
            .Selectable();
    }
}

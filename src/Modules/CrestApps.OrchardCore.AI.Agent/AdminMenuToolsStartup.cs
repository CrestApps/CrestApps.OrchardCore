using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.AdminMenus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list and save the custom admin menus, when the Admin Menu feature is on. The menus are
/// read through the admin menu deployment step, so the Deployment feature, which runs deployment steps, is needed too.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.AdminMenu, OrchardCoreFeatureIds.Deployment)]
public sealed class AdminMenuToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminMenuToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdminMenuToolsStartup(IStringLocalizer<AdminMenuToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListAdminMenusTool>(ListAdminMenusTool.TheName)
            .WithTitle(S["List Admin Menus"])
            .WithDescription(S["Lists the custom admin menus, optionally with their full node tree and the node types available on this site. Call it before changing a menu so existing nodes are kept."])
            .WithCategory(S["Admin"])
            .Selectable();

        services.AddCoreAITool<SaveAdminMenuTool>(SaveAdminMenuTool.TheName)
            .WithTitle(S["Save Admin Menu"])
            .WithDescription(S["Creates a custom admin menu or replaces an existing one, including its name, enabled state and whole node tree. Send the complete tree: nodes left out are removed."])
            .WithCategory(S["Admin"])
            .Selectable();
    }
}

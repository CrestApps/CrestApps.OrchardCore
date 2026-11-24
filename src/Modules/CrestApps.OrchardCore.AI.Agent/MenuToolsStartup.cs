using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Menus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that read a menu and add, update, move and delete one of its items, when the Menu feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Menu)]
public sealed class MenuToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MenuToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MenuToolsStartup(IStringLocalizer<MenuToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetMenuTreeTool>(GetMenuTreeTool.TheName)
            .WithTitle(S["Get Menu Tree"])
            .WithDescription(S["Returns a menu's items as a flat, ordered tree: item ID, type, title, URL or linked content item, parent ID, depth and required permissions. The menu is given by content item ID, alias or title; without one it lists the site's menus."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<SaveMenuItemTool>(SaveMenuItemTool.TheName)
            .WithTitle(S["Save Menu Item"])
            .WithDescription(S["Adds an item to a menu, updates an existing item, or moves it under another parent or after another item, leaving the other items untouched. Supports link, content and HTML menu items; the change is saved as a draft of the menu unless told to publish."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<DeleteMenuItemTool>(DeleteMenuItemTool.TheName)
            .WithTitle(S["Delete Menu Item"])
            .WithDescription(S["Deletes a menu item and all its child items from a menu."])
            .WithCategory(S["Content Management"])
            .RequireApproval()
            .Selectable();
    }
}

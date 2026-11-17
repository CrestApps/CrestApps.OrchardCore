using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Autoroute;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that find the content item behind an address and set the home page, when the Autoroute feature
/// is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Autoroute)]
public sealed class AutorouteToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutorouteToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AutorouteToolsStartup(IStringLocalizer<AutorouteToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<FindContentItemByHandleTool>(FindContentItemByHandleTool.TheName)
            .WithTitle(S["Find Content Item By Handle"])
            .WithDescription(S["Finds the content item behind a URL path, an alias or a handle (slug:about-us, alias:main-menu, contentitemid:...), and returns its ID, type, title and state. A path of / returns the current home page. Use it when the user names a page by its address."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<SetHomepageTool>(SetHomepageTool.TheName)
            .WithTitle(S["Set Homepage"])
            .WithDescription(S["Makes a published content item the site's home page. The item's type must have an Autoroute part with a path and the route must not be disabled."])
            .WithCategory(S["Content Management"])
            .Selectable();
    }
}

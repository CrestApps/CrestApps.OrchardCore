using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Autoroute;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that sets the home page, when the Autoroute feature
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
        // The home page is what every visitor sees first: a site-wide change asks first (review of pull request 1334).
        services.AddCoreAITool<SetHomepageTool>(SetHomepageTool.TheName)
            .WithTitle(S["Set Homepage"])
            .WithDescription(S["Makes a published content item the site's home page. The item's type must have an Autoroute part with a path and the route must not be disabled."])
            .WithCategory(S["Content Management"])
            .AsWrite()
            .RequireApproval()
            .Selectable();
    }
}

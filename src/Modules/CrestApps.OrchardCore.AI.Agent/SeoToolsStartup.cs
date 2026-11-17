using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Seo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that returns the robots.txt content the site serves, when the SEO feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Seo)]
public sealed class SeoToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeoToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SeoToolsStartup(IStringLocalizer<SeoToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetRobotsTxtTool>(GetRobotsTxtTool.TheName)
            .WithTitle(S["Get Robots.txt"])
            .WithDescription(S["Returns the robots.txt content the site currently serves, and says whether it comes from the robots settings or from a physical robots.txt file that overrides them. Use it to check the effect of the robots settings before or after changing them."])
            .WithCategory(S["Search Engine Optimization"])
            .Selectable();
    }
}

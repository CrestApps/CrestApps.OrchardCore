using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Shortcodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that lists the shortcodes of the site, when the Shortcodes feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Shortcodes)]
public sealed class ShortcodesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShortcodesToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ShortcodesToolsStartup(IStringLocalizer<ShortcodesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListShortcodesTool>(ListShortcodesTool.TheName)
            .WithTitle(S["List Shortcodes"])
            .WithDescription(S["Lists every shortcode available on the site (built-in and template-based) with its hint, usage, default value and categories. Use it to learn which shortcodes can be used in HTML or Markdown bodies before writing content."])
            .WithCategory(S["Design"])
            .Selectable();
    }
}

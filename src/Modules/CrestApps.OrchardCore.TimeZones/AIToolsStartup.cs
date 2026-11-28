using CrestApps.Core.AI;
using CrestApps.OrchardCore.TimeZones.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TimeZones;

/// <summary>
/// Registers the time zone map AI tool whenever the Time Zones and AI features are on. It needs
/// <c>ManageTimeZoneMaps</c>, as the Time Zones admin pages do.
/// </summary>
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListTimeZoneMapsTool>(ListTimeZoneMapsTool.TheName)
            .WithTitle(S["List Time Zone Maps"])
            .WithDescription(S["Lists the friendly time zone names shown in time zone pickers and the time zone each maps to, with an optional name filter."])
            .WithCategory(S["Time Zones"])
            .Selectable();
    }
}

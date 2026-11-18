using CrestApps.Core.AI;
using CrestApps.OrchardCore.TimeZones.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TimeZones;

/// <summary>
/// Registers the time zone map AI tools whenever the Time Zones and AI features are on. Each needs
/// <c>ManageTimeZoneMaps</c>, as the Time Zones admin pages do, and deleting a map asks for approval.
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
        services.AddScoped<TimeZoneMapScopeChanges>();

        services.AddCoreAITool<ListTimeZoneMapsTool>(ListTimeZoneMapsTool.TheName)
            .WithTitle(S["List Time Zone Maps"])
            .WithDescription(S["Lists the friendly time zone names shown in time zone pickers and the time zone each maps to, with an optional name filter."])
            .WithCategory(S["Time Zones"])
            .Selectable();

        services.AddCoreAITool<SaveTimeZoneMapTool>(SaveTimeZoneMapTool.TheName)
            .WithTitle(S["Save Time Zone Map"])
            .WithDescription(S["Creates or updates a friendly time zone name for a time zone id. The name must be unique; use listTimeZones to find a valid time zone id."])
            .WithCategory(S["Time Zones"])
            .Selectable();

        services.AddCoreAITool<DeleteTimeZoneMapTool>(DeleteTimeZoneMapTool.TheName)
            .WithTitle(S["Delete Time Zone Map"])
            .WithDescription(S["Deletes a friendly time zone name, so pickers no longer offer it. Records that already store the time zone id keep working."])
            .WithCategory(S["Time Zones"])
            .RequireApproval()
            .Selectable();
    }
}

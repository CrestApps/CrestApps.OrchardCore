using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Assets.AI;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Assets;

/// <summary>
/// Adds the asset AI tools for day-to-day field service when the AI feature is on: the asset list, one asset with its
/// warranty, hierarchy and readings, and recording a meter reading. Asset types are exposed read-only to the generic
/// record tools. Each tool works only for a signed-in person with the permission of the Assets pages.
/// </summary>
[Feature(AssetsConstants.Features.Assets)]
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

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListAssetsTool>(ListAssetsTool.ToolName)
            .WithTitle(S["List assets"])
            .WithDescription(S["Lists assets filtered by type, owner, status and a search on name, number or serial number, with paging."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<GetAssetTool>(GetAssetTool.ToolName)
            .WithTitle(S["Get asset"])
            .WithDescription(S["Gets one asset: type and attributes, owner, location, status, warranty and whether it covers today, the assets it sits in and those inside it, its latest meter readings and, with Fleet, its vehicle details and calendar."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<RecordAssetMeterReadingTool>(RecordAssetMeterReadingTool.ToolName)
            .WithTitle(S["Record asset meter reading"])
            .WithDescription(S["Records a reading of one of an asset's meters (such as hours or kilometres) at a time. A cumulative meter cannot go down."])
            .WithCategory(S["Field Service"])
            .Selectable();

        // The Asset Types page binds its own form (no display driver), so asset types are read-only here.
        services.AddAIRecordKind<AssetType>("assetType", kind => kind
            .WithTitle(S["Asset type"])
            .WithDescription(S["an asset type (furnace, van, ladder...) with its category, the attributes its assets have and the meters they are read on; read-only, changed on the Asset Types page"])
            .WithPermission(AssetsPermissions.ManageAssets)
            .UseCatalogManager<AssetTypeManager>()
            .WithProjection(type => new
            {
                id = type.ItemId,
                name = type.Name,
                category = type.Category.ToString(),
                description = type.Description,
                attributes = (type.Attributes ?? new List<AssetAttributeDefinition>()).Select(attribute => new { name = attribute.Name, kind = attribute.Kind.ToString() }).ToList(),
                meters = (type.Meters ?? new List<MeterDefinition>()).Select(meter => new { name = meter.Name, unit = meter.Unit, cumulative = meter.Cumulative }).ToList(),
            })
            .ReadOnly());
    }
}

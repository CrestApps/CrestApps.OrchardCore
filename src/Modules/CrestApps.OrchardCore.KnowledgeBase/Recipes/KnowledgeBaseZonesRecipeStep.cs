using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.KnowledgeBase.Recipes;

/// <summary>
/// Adds the help center's zones (above the page, beside it and after the article) to the zones widgets may be placed in,
/// under <b>Design &gt; Settings &gt; Zones</b>, keeping the site's own. The help center's page and an article's page show
/// these zones themselves, so their widgets show whatever the theme.
/// </summary>
internal sealed class KnowledgeBaseZonesRecipeStep : NamedRecipeStepHandler
{
    internal const string Name = "KnowledgeBaseZones";

    private readonly ISiteService _siteService;

    public KnowledgeBaseZonesRecipeStep(ISiteService siteService)
        : base(Name)
    {
        _siteService = siteService;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var site = await _siteService.LoadSiteSettingsAsync();

        if (AddZones(site.Properties))
        {
            await _siteService.UpdateSiteSettingsAsync(site);
        }
    }

    /// <summary>
    /// Adds the help center's zones to the Layers settings held in the site's properties.
    /// </summary>
    /// <param name="properties">The site's properties.</param>
    /// <returns>Whether a zone was added.</returns>
    internal static bool AddZones(JsonObject properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        if (properties["LayerSettings"] is not JsonObject settings)
        {
            properties["LayerSettings"] = settings = [];
        }

        if (settings["Zones"] is not JsonArray zones)
        {
            settings["Zones"] = zones = [];
        }

        var existing = zones.Select(zone => zone?.GetValue<string>()).Where(zone => zone is not null).ToHashSet(StringComparer.Ordinal);
        var added = false;

        foreach (var zone in new[] { KnowledgeBaseConstants.Zones.Top, KnowledgeBaseConstants.Zones.Sidebar, KnowledgeBaseConstants.Zones.AfterContent })
        {
            if (existing.Add(zone))
            {
                zones.Add(zone);
                added = true;
            }
        }

        return added;
    }
}

/// <summary>
/// Describes the KnowledgeBaseZones recipe step to CrestApps Recipes.
/// </summary>
internal sealed class KnowledgeBaseZonesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => KnowledgeBaseZonesRecipeStep.Name;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Adds the help center's widget zones (KnowledgeBaseTop, KnowledgeBaseSidebar and KnowledgeBaseAfterContent) to the zones of Design > Settings > Zones, keeping the site's own zones.")
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(KnowledgeBaseZonesRecipeStep.Name).Description("Recipe step discriminator. Must be 'KnowledgeBaseZones'.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}

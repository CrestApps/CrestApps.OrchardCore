using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Agent.Taxonomies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that read the terms of a taxonomy and add, update, move and delete one term, when the Taxonomies
/// feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Taxonomies)]
public sealed class TaxonomiesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxonomiesToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxonomiesToolsStartup(IStringLocalizer<TaxonomiesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        // The save tool checks the content of a term the way createOrUpdateContentItem checks a content item. The
        // Taxonomies feature can be on without the Contents feature, so the helper is registered here too.
        services.TryAddScoped<IContentItemPayloadAssistanceService, DefaultContentItemPayloadAssistanceService>();

        services.AddCoreAITool<GetTaxonomyTermsTool>(GetTaxonomyTermsTool.TheName)
            .WithTitle(S["Get Taxonomy Terms"])
            .WithDescription(S["Returns the terms of a taxonomy as a flat, ordered tree: term ID, title, parent ID, depth and full path. Use it to find term IDs before tagging content or changing the tree; the taxonomy is given by content item ID, alias or title."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<SaveTaxonomyTermTool>(SaveTaxonomyTermTool.TheName)
            .WithTitle(S["Save Taxonomy Term"])
            .WithDescription(S["Adds a term to a taxonomy, updates an existing term, or moves it under another parent or to another position, without touching the other terms. New terms use the taxonomy's term content type."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddCoreAITool<DeleteTaxonomyTermTool>(DeleteTaxonomyTermTool.TheName)
            .WithTitle(S["Delete Taxonomy Term"])
            .WithDescription(S["Deletes a term and all its child terms from a taxonomy. Content items that reference the term keep the stale ID and are not updated."])
            .WithCategory(S["Content Management"])
            .RequireApproval()
            .Selectable();
    }
}

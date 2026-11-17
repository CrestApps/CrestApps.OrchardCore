using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Flows;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;
using OrchardCore.Taxonomies.Models;

namespace CrestApps.OrchardCore.AI.Agent.Taxonomies;

/// <summary>
/// Deletes one term of a taxonomy with the terms under it, and saves the taxonomy through the content manager.
/// </summary>
public sealed class DeleteTaxonomyTermTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteTaxonomyTerm";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "taxonomyContentItemId": {
          "type": "string",
          "description": "The content item ID of the taxonomy, from getTaxonomyTerms."
        },
        "termId": {
          "type": "string",
          "description": "The ID of the term to delete, from getTaxonomyTerms."
        },
        "publish": {
          "type": "boolean",
          "description": "Whether the taxonomy is published after the change, which makes it live. The default is false: when the Taxonomy content type can have drafts, the change is saved in a draft of the taxonomy."
        }
      },
      "required": [
        "taxonomyContentItemId",
        "termId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a term and all its child terms from a taxonomy. Content items that reference the term keep the stale ID and are not updated. The change is saved as a draft of the taxonomy unless publish is true.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.ManageTaxonomies))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("taxonomyContentItemId", out var taxonomyContentItemId))
        {
            return "The taxonomyContentItemId argument is required.";
        }

        if (!arguments.TryGetFirstString("termId", out var termId))
        {
            return "The termId argument is required.";
        }

        var publish = arguments.GetFirstValueOrDefault("publish", false);
        var taxonomy = await services.GetRequiredService<IContentManager>().GetAsync(taxonomyContentItemId, VersionOptions.Latest);

        if (taxonomy is null)
        {
            return $"No content item with the ID '{taxonomyContentItemId}' was found. Use getTaxonomyTerms to list the taxonomies.";
        }

        if (publish && !await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.PublishContent, taxonomy))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!taxonomy.TryGet<TaxonomyPart>(out _))
        {
            return $"Nothing was deleted: content item '{taxonomy.ContentItemId}' is a '{taxonomy.ContentType}', not a taxonomy.";
        }

        var tree = ContentItemListTree.Taxonomy;
        var root = NestedContentItemTree.ToJson(services, taxonomy);
        var entries = tree.GetEntries(root);
        var entry = NestedContentItemTree.Find(entries, termId);

        if (entry is null)
        {
            return $"Nothing was deleted: taxonomy '{taxonomy.ContentItemId}' has no term with the ID '{termId}'. Use getTaxonomyTerms to get the IDs.";
        }

        var deletedChildTerms = ContentItemListTree.CountDescendants(entries, entry);

        entry.Container.Items.RemoveAt(entry.Position);

        var taxonomyDefinition = await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(taxonomy.ContentType);
        var (saved, saveError) = await tree.SaveAsync(services, taxonomy, taxonomyDefinition, root, publish);

        if (saveError is not null)
        {
            return saveError.Replace("Nothing was saved", "Nothing was deleted", StringComparison.Ordinal);
        }

        return AgentToolResults.Json(new
        {
            deleted = true,
            taxonomyContentItemId = saved.ContentItemId,
            termId = NestedContentItemTree.GetString(entry.Item, nameof(ContentItem.ContentItemId)),
            title = NestedContentItemTree.GetString(entry.Item, nameof(ContentItem.DisplayText)),
            deletedChildTerms,
            published = saved.Published,
            note = $"{tree.GetStateNote(saved)} Content items that reference a deleted term keep its ID and are not updated.",
        });
    }
}

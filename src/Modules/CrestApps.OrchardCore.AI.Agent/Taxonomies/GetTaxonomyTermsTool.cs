using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Flows;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Taxonomies.Models;

namespace CrestApps.OrchardCore.AI.Agent.Taxonomies;

/// <summary>
/// Returns the terms of a taxonomy as a flat, ordered tree, or lists the taxonomies of the site.
/// </summary>
public sealed class GetTaxonomyTermsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getTaxonomyTerms";

    internal const string TaxonomyContentType = "Taxonomy";

    private const int MaxItems = 500;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "taxonomy": {
          "type": "string",
          "description": "The taxonomy: its content item ID, its alias (for example categories) or its title. Leave it out to list the taxonomies of the site."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns the terms of a taxonomy as a flat, ordered tree: term ID, title, parent ID, depth and full path. Use it to find term IDs before tagging content or changing the tree; the taxonomy is given by content item ID, alias or title, and without one the taxonomies of the site are listed. It reads the latest version of the taxonomy, which is a draft when the taxonomy has unpublished changes.";

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

        if (AgentToolAuthorization.GetCaller(services) is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("taxonomy", out var taxonomyName))
        {
            return await ListTaxonomiesAsync(services);
        }

        var (taxonomy, error) = await ContentItemLookup.FindAsync(services, TaxonomyContentType, taxonomyName);

        if (error is not null)
        {
            return error;
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.ViewContent, taxonomy))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!taxonomy.TryGet<TaxonomyPart>(out var taxonomyPart))
        {
            return $"Content item '{taxonomy.ContentItemId}' is a '{taxonomy.ContentType}', not a taxonomy. Call this tool without a taxonomy to list the taxonomies.";
        }

        var root = NestedContentItemTree.ToJson(services, taxonomy);
        var entries = ContentItemListTree.Taxonomy.GetEntries(root);
        var titles = new List<string>();
        var items = new List<object>();

        foreach (var entry in entries.Take(MaxItems))
        {
            var title = NestedContentItemTree.GetString(entry.Item, nameof(ContentItem.DisplayText));

            // The outline lists a term before the terms under it, so the titles down to this term make its path.
            while (titles.Count >= entry.Container.Depth)
            {
                titles.RemoveAt(titles.Count - 1);
            }

            titles.Add(title);

            items.Add(new
            {
                termId = NestedContentItemTree.GetString(entry.Item, nameof(ContentItem.ContentItemId)),
                title,
                parentTermId = entry.Container.OwnerId,
                depth = entry.Container.Depth,
                position = entry.Position,
                path = string.Join(" / ", titles),
            });
        }

        return AgentToolResults.Json(new
        {
            taxonomyContentItemId = taxonomy.ContentItemId,
            displayText = taxonomy.DisplayText,
            termContentType = taxonomyPart.TermContentType,
            published = taxonomy.Published,
            note = taxonomy.Published
                ? null
                : "This is a draft of the taxonomy. The site uses the published version until the taxonomy is published.",
            items,
            total = entries.Count,
            truncated = entries.Count > MaxItems
                ? true
                : (bool?)null,
        });
    }

    private static async Task<string> ListTaxonomiesAsync(IServiceProvider services)
    {
        var taxonomies = new List<object>();
        var found = false;

        foreach (var taxonomy in await ContentItemLookup.ListLatestAsync(services, TaxonomyContentType))
        {
            found = true;

            if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.ViewContent, taxonomy))
            {
                continue;
            }

            taxonomies.Add(new
            {
                taxonomyContentItemId = taxonomy.ContentItemId,
                displayText = taxonomy.DisplayText,
                termContentType = taxonomy.TryGet<TaxonomyPart>(out var taxonomyPart)
                    ? taxonomyPart.TermContentType
                    : null,
                published = taxonomy.Published,
            });
        }

        if (found && taxonomies.Count == 0)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        return AgentToolResults.Json(new
        {
            taxonomies,
            total = taxonomies.Count,
        });
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Agent.Flows;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;
using OrchardCore.Taxonomies.Models;

namespace CrestApps.OrchardCore.AI.Agent.Taxonomies;

/// <summary>
/// Adds, updates or moves one term of a taxonomy, and saves the taxonomy through the content manager. The other terms
/// are left as they are.
/// </summary>
public sealed class SaveTaxonomyTermTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveTaxonomyTerm";

    private const string TitlePartName = "TitlePart";

    private static readonly string[] _reservedContentNames =
    [
        nameof(ContentItem.ContentItemId),
        nameof(ContentItem.ContentItemVersionId),
        nameof(ContentItem.DisplayText),
        nameof(TermPart),
        nameof(TaxonomyPart.Terms),
    ];

    private static readonly JsonMergeSettings _replaceArrays = new()
    {
        MergeArrayHandling = MergeArrayHandling.Replace,
    };

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
          "description": "The ID of the term to update or move, from getTaxonomyTerms. Leave it out to add a new term."
        },
        "title": {
          "type": "string",
          "description": "The title of the term. Required for a new term."
        },
        "content": {
          "type": "object",
          "description": "The other parts and fields of the term, in the Orchard Core content item JSON shape, for example {\"Category\": {\"Icon\": {\"Text\": \"fa-leaf\"}}}. Give only what changes; arrays are replaced whole. It cannot hold the title, the ID or child terms."
        },
        "parentTermId": {
          "type": "string",
          "description": "The ID of the term that takes this term under it. Give the taxonomy's own content item ID for the top level. Leave it out to add at the top level, or to keep an existing term where it is."
        },
        "position": {
          "type": "integer",
          "minimum": 0,
          "description": "The position among the terms of the same parent, starting at 0. Leave it out to add at the end, or to keep the position of an existing term."
        },
        "publish": {
          "type": "boolean",
          "description": "Whether the taxonomy is published after the change, which makes it live. The default is false: when the Taxonomy content type can have drafts, the change is saved in a draft of the taxonomy."
        }
      },
      "required": [
        "taxonomyContentItemId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Adds a term to a taxonomy, updates an existing term, or moves it under another parent or to another position, without touching the other terms. New terms use the taxonomy's term content type. The change is saved as a draft of the taxonomy unless publish is true.";

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

        if (!AgentToolArguments.TryReadJsonObject(arguments, "content", out var content))
        {
            return "Nothing was saved: content must be a JSON object.";
        }

        var hasPosition = arguments.TryGetFirst<int>("position", out var position);

        if (hasPosition && position < 0)
        {
            return "Nothing was saved: position must be 0 or more.";
        }

        var publish = arguments.GetFirstValueOrDefault("publish", false);
        var contentManager = services.GetRequiredService<IContentManager>();
        var taxonomy = await contentManager.GetAsync(taxonomyContentItemId, VersionOptions.Latest);

        if (taxonomy is null)
        {
            return $"No content item with the ID '{taxonomyContentItemId}' was found. Use getTaxonomyTerms to list the taxonomies.";
        }

        if (publish && !await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.PublishContent, taxonomy))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!taxonomy.TryGet<TaxonomyPart>(out var taxonomyPart))
        {
            return $"Nothing was saved: content item '{taxonomy.ContentItemId}' is a '{taxonomy.ContentType}', not a taxonomy.";
        }

        var contentDefinitionManager = services.GetRequiredService<IContentDefinitionManager>();
        var tree = ContentItemListTree.Taxonomy;
        var root = NestedContentItemTree.ToJson(services, taxonomy);
        var entries = tree.GetEntries(root);
        var hasTitle = arguments.TryGetFirstString("title", out var title);
        var hasParent = arguments.TryGetFirstString("parentTermId", out var parentTermId);

        NestedContentItemEntry entry = null;
        JsonObject term;
        ContentTypeDefinition termDefinition;

        if (arguments.TryGetFirstString("termId", out var termId))
        {
            entry = NestedContentItemTree.Find(entries, termId);

            if (entry is null)
            {
                return $"Nothing was saved: taxonomy '{taxonomy.ContentItemId}' has no term with the ID '{termId}'. Use getTaxonomyTerms to get the IDs.";
            }

            term = entry.Item;

            var termType = NestedContentItemTree.GetString(term, nameof(ContentItem.ContentType));

            termDefinition = string.IsNullOrEmpty(termType)
                ? null
                : await contentDefinitionManager.GetTypeDefinitionAsync(termType);
        }
        else
        {
            if (!hasTitle)
            {
                return "Nothing was saved: give title to add a term, or termId to update or move one.";
            }

            termDefinition = string.IsNullOrEmpty(taxonomyPart.TermContentType)
                ? null
                : await contentDefinitionManager.GetTypeDefinitionAsync(taxonomyPart.TermContentType);

            if (termDefinition is null)
            {
                return $"Nothing was saved: the term content type '{taxonomyPart.TermContentType}' of the taxonomy does not exist. Set the term content type of the taxonomy first.";
            }

            // As the taxonomy editor does, a new term starts from the term content type and is tied to its taxonomy.
            var newTerm = await contentManager.NewAsync(termDefinition.Name);

            newTerm.Weld<TermPart>();
            newTerm.Alter<TermPart>(part => part.TaxonomyContentItemId = taxonomy.ContentItemId);

            term = NestedContentItemTree.ToJson(services, newTerm);
        }

        if (content is not null)
        {
            var error = await GetContentErrorAsync(services, termDefinition, term, content, cancellationToken);

            if (error is not null)
            {
                return error;
            }

            term.Merge(content, _replaceArrays);
        }

        if (hasTitle)
        {
            title = title.Trim();
            term[nameof(ContentItem.DisplayText)] = title;

            // The title part editor writes the title to the part and to the display text.
            if (termDefinition?.Parts.Any(part => string.Equals(part.PartDefinition?.Name, TitlePartName, StringComparison.Ordinal)) == true)
            {
                if (term[TitlePartName] is not JsonObject titlePart)
                {
                    titlePart = [];
                    term[TitlePartName] = titlePart;
                }

                titlePart["Title"] = title;
            }
        }

        var updates = entry is not null && (hasTitle || content is not null);
        var moves = entry is null || hasParent || hasPosition;

        if (updates)
        {
            // As the taxonomy editor does when a term is edited, the term is tied to its taxonomy.
            term[nameof(TermPart)] = new JsonObject
            {
                [nameof(TermPart.TaxonomyContentItemId)] = taxonomy.ContentItemId,
            };
        }

        if (!updates && !moves)
        {
            return "Nothing was saved: give title or content to change the term, or parentTermId or position to move it.";
        }

        var target = entry?.Container;
        var newPosition = entry?.Position ?? 0;

        if (moves)
        {
            if (hasParent || entry is null)
            {
                target = tree.FindTarget(root, entries, parentTermId);
            }

            if (target is null)
            {
                return $"Nothing was saved: taxonomy '{taxonomy.ContentItemId}' has no term with the ID '{parentTermId}' to put the term under. Use getTaxonomyTerms to get the IDs.";
            }

            if (entry is not null && ContentItemListTree.IsInside(target.Owner, entry.Item))
            {
                return "Nothing was saved: a term cannot be moved under itself or under one of its own terms.";
            }

            newPosition = ContentItemListTree.Place(entry, term, target, hasPosition ? position : null);
        }

        var taxonomyDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(taxonomy.ContentType);
        var (saved, saveError) = await tree.SaveAsync(services, taxonomy, taxonomyDefinition, root, publish);

        if (saveError is not null)
        {
            return saveError;
        }

        var action = "added";

        if (entry is not null)
        {
            action = !moves
                ? "updated"
                : updates
                    ? "updatedAndMoved"
                    : "moved";
        }

        return AgentToolResults.Json(new
        {
            saved = true,
            action,
            taxonomyContentItemId = saved.ContentItemId,
            termId = NestedContentItemTree.GetString(term, nameof(ContentItem.ContentItemId)),
            title = NestedContentItemTree.GetString(term, nameof(ContentItem.DisplayText)),
            parentTermId = target.OwnerId,
            position = newPosition,
            published = saved.Published,
            note = tree.GetStateNote(saved),
        });
    }

    /// <summary>
    /// Says why the content cannot be merged into the term: it changes what the tool owns, or it does not fit the term
    /// content type.
    /// </summary>
    private static async Task<string> GetContentErrorAsync(
        IServiceProvider services,
        ContentTypeDefinition termDefinition,
        JsonObject term,
        JsonObject content,
        CancellationToken cancellationToken)
    {
        var reserved = content
            .Select(property => property.Key)
            .Where(name => _reservedContentNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (reserved.Count > 0)
        {
            return $"Nothing was saved: content cannot hold {string.Join(", ", reserved)}. Use title for the title, and call this tool once for each term.";
        }

        var termType = NestedContentItemTree.GetString(term, nameof(ContentItem.ContentType));

        if (termDefinition is null)
        {
            return $"Nothing was saved: the content type '{termType}' of the term no longer exists, so its content cannot be checked.";
        }

        var contentTypeName = content
            .Select(property => property.Key)
            .FirstOrDefault(name => string.Equals(name, nameof(ContentItem.ContentType), StringComparison.OrdinalIgnoreCase));

        if (contentTypeName is not null)
        {
            if (!string.Equals(NestedContentItemTree.GetString(content, contentTypeName), termDefinition.Name, StringComparison.OrdinalIgnoreCase))
            {
                return $"Nothing was saved: the term is a '{termDefinition.Name}' and its content type cannot be changed.";
            }

            content.Remove(contentTypeName);
        }

        // The check reads the content type from the JSON, as createOrUpdateContentItem gives it.
        var shape = content.DeepClone().AsObject();

        shape[nameof(ContentItem.ContentType)] = termDefinition.Name;

        return await ContentItemPayloadCheck.GetErrorAsync(services, termDefinition, shape, "term content", "content", cancellationToken);
    }
}

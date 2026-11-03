using CrestApps.OrchardCore.Seo.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Seo.Models;
using OrchardCore.Shortcodes.Services;
using Shortcodes;

namespace CrestApps.OrchardCore.Seo.Providers;

/// <summary>
/// Merges the JSON-LD an editor typed in the <c>Google Schema</c> field of Orchard Core's SEO meta part into the
/// structured data, so the page gets one JSON-LD script instead of two. A node with the content item's type and no
/// <c>@id</c> overrides the properties of the content item's node; other nodes, and every node of a content item that has no node of its own, are added to the page.
/// </summary>
internal sealed class EditorJsonLdStructuredDataProvider : IStructuredDataProvider
{
    private readonly IShortcodeService _shortcodeService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EditorJsonLdStructuredDataProvider"/> class.
    /// </summary>
    /// <param name="shortcodeService">The shortcode service, which processes the shortcodes the editor typed.</param>
    /// <param name="logger">The logger.</param>
    public EditorJsonLdStructuredDataProvider(
        IShortcodeService shortcodeService,
        ILogger<EditorJsonLdStructuredDataProvider> logger)
    {
        _shortcodeService = shortcodeService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => StructuredDataProviderOrder.EditorJsonLd;

    /// <inheritdoc/>
    public async Task BuildAsync(StructuredDataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ContentItem is null ||
            !context.ContentItem.TryGet<SeoMetaPart>(out var part) ||
            string.IsNullOrWhiteSpace(part.GoogleSchema) ||
            (context.ContentItem.TryGet<SchemaOrgPart>(out var schemaOrgPart) && schemaOrgPart.Disabled))
        {
            // An item whose structured data is turned off keeps its JSON-LD in the script Orchard Core writes.
            return;
        }

        // Without a node for the content item, the editor's nodes are still the page's, but an answer has nothing to merge them into.
        if (context.MainEntity is null && context.Purpose != StructuredDataPurpose.Page)
        {
            return;
        }

        var json = await _shortcodeService.ProcessAsync(part.GoogleSchema, new Context
        {
            ["ContentItem"] = context.ContentItem,
        });

        if (!JsonLd.TryParseNodes(json, out var nodes))
        {
            _logger.LogWarning("The JSON-LD of the content item '{ContentItemId}' is not valid JSON and is ignored.", context.ContentItem.ContentItemId);

            return;
        }

        var mainType = context.MainEntity is null ? null : JsonLd.GetType(context.MainEntity);

        foreach (var node in nodes)
        {
            var hasId = JsonLd.GetString(node, StructuredDataConstants.Keywords.Id) is not null;
            var type = JsonLd.GetType(node);

            if (context.MainEntity is not null && !hasId && (type is null || string.Equals(type, mainType, StringComparison.Ordinal) || nodes.Count == 1))
            {
                JsonLd.Merge(context.MainEntity, node);
                mainType = JsonLd.GetType(context.MainEntity);

                continue;
            }

            if (context.Purpose == StructuredDataPurpose.Page)
            {
                context.Graph.Add(node);
            }
        }
    }
}

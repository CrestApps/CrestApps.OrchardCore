using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Seo.Models;
using CrestApps.OrchardCore.Seo.SchemaOrg;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.Seo.Providers;

/// <summary>
/// Sets the schema.org properties the Schema.org part's settings map: from the content item's fields and values, fixed
/// values, Liquid, and the values editors enter on the part.
/// </summary>
internal sealed class SchemaOrgPartStructuredDataProvider : IStructuredDataProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly SchemaOrgContentSources _contentSources;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaOrgPartStructuredDataProvider"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="contentSources">The values of content items that properties can take.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager.</param>
    /// <param name="logger">The logger.</param>
    public SchemaOrgPartStructuredDataProvider(
        IContentDefinitionManager contentDefinitionManager,
        SchemaOrgContentSources contentSources,
        ILiquidTemplateManager liquidTemplateManager,
        ILogger<SchemaOrgPartStructuredDataProvider> logger)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentSources = contentSources;
        _liquidTemplateManager = liquidTemplateManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => StructuredDataProviderOrder.SchemaOrgPart;

    /// <inheritdoc/>
    public async Task BuildAsync(StructuredDataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ContentItem is null || context.MainEntity is null)
        {
            return;
        }

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(context.ContentItem.ContentType);
        var partDefinition = SchemaOrgPartDefinitions.Find(definition);

        if (partDefinition is null)
        {
            return;
        }

        var settings = partDefinition.GetSettings<SchemaOrgPartSettings>();

        if (settings.Properties is not { Count: > 0 })
        {
            return;
        }

        var part = context.ContentItem.TryGet<SchemaOrgPart>(out var schemaOrgPart) ? schemaOrgPart : null;
        var typeName = JsonLd.GetType(context.MainEntity);

        foreach (var mapping in settings.Properties)
        {
            if (!SchemaOrgValueWriter.IsValidPropertyName(mapping?.Name))
            {
                continue;
            }

            var raw = await ReadAsync(mapping, part, context, definition);
            var property = SchemaOrgVocabulary.GetProperty(typeName, mapping.Name);
            var value = SchemaOrgValueWriter.Convert(raw, property, context);

            SchemaOrgValueWriter.Set(
                context.MainEntity,
                mapping.Name,
                value,
                path => SchemaOrgVocabulary.GetProperty(typeName, path)?.ObjectType);
        }
    }

    private async Task<JsonNode> ReadAsync(SchemaOrgPropertyMapping mapping, SchemaOrgPart part, StructuredDataContext context, ContentTypeDefinition definition)
    {
        switch (mapping.Source)
        {
            case SchemaOrgValueSource.Content:
                return await _contentSources.ReadAsync(mapping.Value, context, definition);
            case SchemaOrgValueSource.Fixed:
                return string.IsNullOrWhiteSpace(mapping.Value) ? null : JsonValue.Create(mapping.Value);
            case SchemaOrgValueSource.Editor:
                return part?.Values is not null && part.Values.TryGetValue(mapping.Name, out var entered) && !string.IsNullOrWhiteSpace(entered)
                    ? JsonValue.Create(entered)
                    : null;
            case SchemaOrgValueSource.Liquid:
                var rendered = await RenderAsync(mapping, context);

                return string.IsNullOrWhiteSpace(rendered) ? null : JsonValue.Create(rendered);
            default:
                return null;
        }
    }

    private async Task<string> RenderAsync(SchemaOrgPropertyMapping mapping, StructuredDataContext context)
    {
        if (string.IsNullOrWhiteSpace(mapping.Value))
        {
            return null;
        }

        var properties = new Dictionary<string, FluidValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["ContentItem"] = ObjectValue.Create(context.ContentItem),
            ["Url"] = StringValue.Create(context.Url ?? string.Empty),
            ["BaseUrl"] = StringValue.Create(context.BaseUrl),
        };

        try
        {
            return await _liquidTemplateManager.RenderStringAsync(mapping.Value, NullEncoder.Default, context.ContentItem, properties);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "The Liquid of the schema.org property '{Property}' of the content type '{ContentType}' failed to render.",
                mapping.Name,
                context.ContentItem.ContentType);

            return null;
        }
    }
}

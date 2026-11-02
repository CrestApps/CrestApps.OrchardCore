using CrestApps.OrchardCore.Seo.Models;
using CrestApps.OrchardCore.Seo.Services;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.Seo.Providers;

/// <summary>
/// Sets the schema.org properties a content type maps with Liquid in its structured data settings.
/// </summary>
internal sealed class ContentTypeMappingStructuredDataProvider : IStructuredDataProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentTypeMappingStructuredDataProvider"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager.</param>
    /// <param name="logger">The logger.</param>
    public ContentTypeMappingStructuredDataProvider(
        IContentDefinitionManager contentDefinitionManager,
        ILiquidTemplateManager liquidTemplateManager,
        ILogger<ContentTypeMappingStructuredDataProvider> logger)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _liquidTemplateManager = liquidTemplateManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => StructuredDataProviderOrder.ContentTypeMapping;

    /// <inheritdoc/>
    public async Task BuildAsync(StructuredDataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ContentItem is null || context.MainEntity is null)
        {
            return;
        }

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(context.ContentItem.ContentType);
        var settings = definition?.GetSettings<StructuredDataTypeSettings>();
        var properties = StructuredDataPropertyMap.Parse(settings?.Properties);

        if (properties.Count == 0)
        {
            return;
        }

        var liquidProperties = new Dictionary<string, FluidValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["ContentItem"] = new ObjectValue(context.ContentItem),
            ["Url"] = new StringValue(context.Url ?? string.Empty),
            ["BaseUrl"] = new StringValue(context.BaseUrl),
        };

        foreach (var property in properties)
        {
            string value;

            try
            {
                value = await _liquidTemplateManager.RenderStringAsync(property.Value, NullEncoder.Default, context.ContentItem, liquidProperties);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "The Liquid of the schema.org property '{Property}' of the content type '{ContentType}' failed to render.",
                    property.Key,
                    context.ContentItem.ContentType);

                continue;
            }

            StructuredDataPropertyMap.Apply(context.MainEntity, property.Key, value);
        }
    }
}

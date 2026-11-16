using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.ContentFields.ConditionalFields;

/// <summary>
/// Leaves out of an item's display the fields its rules hide, so an answer kept from before a later edit (an outage
/// question answered before the category changed) isn't shown as if it applied.
/// </summary>
internal sealed class ConditionalFieldsPlacementProvider : IShapePlacementProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ConditionalFieldsService _service;

    public ConditionalFieldsPlacementProvider(IContentDefinitionManager contentDefinitionManager, ConditionalFieldsService service)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _service = service;
    }

    public async Task<IPlacementInfoResolver> BuildPlacementInfoResolverAsync(IBuildShapeContext context)
    {
        if (context is not BuildDisplayContext || GetContentItem(context.Shape) is not { } contentItem)
        {
            return null;
        }

        var type = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        if (ConditionalFieldsService.GetRules(type).Count == 0)
        {
            return null;
        }

        var hidden = await _service.GetHiddenAsync(contentItem, type);

        if (hidden.Count == 0)
        {
            return null;
        }

        // A field's display shape is told apart by "{TypePartName}-{FieldName}".
        return new Resolver(hidden.Select(key => key.Replace('.', '-')).ToHashSet(StringComparer.Ordinal));
    }

    // The content display manager puts the item it builds on the content shape's properties.
    private static ContentItem GetContentItem(IShape shape)
        => shape?.Properties.TryGetValue("ContentItem", out var value) == true ? value as ContentItem : null;

    private sealed class Resolver : IPlacementInfoResolver
    {
        private readonly HashSet<string> _hidden;

        public Resolver(HashSet<string> hidden)
        {
            _hidden = hidden;
        }

        public PlacementInfo ResolvePlacement(ShapePlacementContext placementContext)
            => placementContext.Differentiator is { } differentiator && _hidden.Contains(differentiator)
                ? PlacementInfo.Hidden
                : null;
    }
}

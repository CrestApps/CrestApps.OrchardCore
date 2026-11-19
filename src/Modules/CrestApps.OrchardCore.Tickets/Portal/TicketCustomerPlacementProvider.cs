using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Tickets.Portal;

/// <summary>
/// Hides, in the customer's editor of a ticket type (<see cref="TicketCustomerEditor"/>), every part and field editor
/// but the subject and the fields customers fill in.
/// </summary>
internal sealed class TicketCustomerPlacementProvider : IShapePlacementProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public TicketCustomerPlacementProvider(IHttpContextAccessor httpContextAccessor, IContentDefinitionManager contentDefinitionManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<IPlacementInfoResolver> BuildPlacementInfoResolverAsync(IBuildShapeContext context)
    {
        if (context is not BuildEditorContext
            || !TicketCustomerEditor.IsActive(_httpContextAccessor.HttpContext)
            || GetContentItem(context.Shape) is not { } contentItem)
        {
            return null;
        }

        var type = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        if (type is null || !type.Parts.Any(part => part.PartDefinition.Name == TicketsConstants.PartName))
        {
            return null;
        }

        var (shown, all) = TicketCustomerEditor.Shapes(type);

        return new Resolver(shown, all);
    }

    // The content display manager puts the item it builds on the content shape's properties.
    private static ContentItem GetContentItem(IShape shape)
        => shape?.Properties.TryGetValue("ContentItem", out var value) == true ? value as ContentItem : null;

    private sealed class Resolver : IPlacementInfoResolver
    {
        private readonly HashSet<string> _shown;
        private readonly HashSet<string> _all;

        public Resolver(HashSet<string> shown, HashSet<string> all)
        {
            _shown = shown;
            _all = all;
        }

        public PlacementInfo ResolvePlacement(ShapePlacementContext placementContext)
            => placementContext.Differentiator is { } differentiator && _all.Contains(differentiator) && !_shown.Contains(differentiator)
                ? PlacementInfo.Hidden
                : null;
    }
}

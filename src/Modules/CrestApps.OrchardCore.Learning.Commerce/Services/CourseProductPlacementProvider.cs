using CrestApps.OrchardCore.Learning.Programs;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Learning.Commerce.Services;

/// <summary>
/// Hides the product box (the price and Add to cart) on the page of a course or learning path that is not for sale: a
/// free course leads with its own title, summary and Enroll button instead of "Not sold online".
/// </summary>
internal sealed class CourseProductPlacementProvider : IShapePlacementProvider
{
    private static readonly HashSet<string> _productShapes = new(StringComparer.Ordinal) { "ProductSummary", "ProductSpecifications" };

    public Task<IPlacementInfoResolver> BuildPlacementInfoResolverAsync(IBuildShapeContext context)
    {
        if (context is not BuildDisplayContext display ||
            display.DisplayType != "Detail" ||
            !(display.Shape?.Properties.TryGetValue("ContentItem", out var value) == true && value is ContentItem item) ||
            item.ContentType is not (LearningConstants.ContentTypes.Course or ProgramsConstants.LearningPathContentType) ||
            IsForSale(item))
        {
            return Task.FromResult<IPlacementInfoResolver>(null);
        }

        return Task.FromResult<IPlacementInfoResolver>(new Resolver());
    }

    // A course is for sale with a price; an item without one is simply not for sale (the part's price is optional).
    private static bool IsForSale(ContentItem item)
        => item.TryGet<ProductPart>(out var product) && (product.Price is not null || product.PriceTiers.Count > 0);

    private sealed class Resolver : IPlacementInfoResolver
    {
        public PlacementInfo ResolvePlacement(ShapePlacementContext placementContext)
            => _productShapes.Contains(placementContext.ShapeType) ? PlacementInfo.Hidden : null;
    }
}

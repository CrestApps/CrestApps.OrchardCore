using OrchardCore.DisplayManagement.Descriptors;

namespace CrestApps.OrchardCore.Commerce.Services;

/// <summary>
/// Works around the admin theme's <c>Pager_PageSizeSelector</c> template, which never renders the page size selector.
/// </summary>
/// <remarks>
/// The pager passes the allowed page sizes to the selector as a shape property named <c>Items</c>, but every shape
/// already has a CLR property called <c>Items</c> (its child shapes). The theme's template reads <c>Model.Items</c>,
/// which binds to that CLR property, gets an empty list, and renders nothing, so no listing shows the selector even
/// when <b>Allow users to change the page size</b> is on. This adds an alternate whose template reads the sizes from
/// the shape's properties instead. Remove it once the Orchard Core version in use includes
/// OrchardCMS/OrchardCore#20007, which renames the argument to <c>PageSizes</c> and fixes the themes.
/// </remarks>
internal sealed class PagerPageSizeSelectorShapeTableProvider : IShapeTableProvider
{
    internal const string ShapeType = "Pager_PageSizeSelector";
    internal const string Alternate = "Pager_PageSizeSelector__Properties";

    /// <inheritdoc/>
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe(ShapeType)
            .OnDisplaying(displaying => displaying.Shape.Metadata.Alternates.Add(Alternate));

        return ValueTask.CompletedTask;
    }
}

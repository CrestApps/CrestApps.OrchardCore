using CrestApps.OrchardCore.Core.Models;
using OrchardCore.DisplayManagement.Descriptors;

namespace CrestApps.OrchardCore.Commerce.Services;

/// <summary>
/// Adds the <c>CommerceAdminListFilters__{ListName}</c> alternate to the filters of a commerce admin list, so a theme
/// can override one list's filters.
/// </summary>
internal sealed class AdminListFiltersShapeTableProvider : IShapeTableProvider
{
    /// <inheritdoc/>
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe(CommerceConstants.Shapes.AdminListFilters)
            .OnDisplaying(displaying =>
            {
                if (displaying.Shape is AdminListFiltersViewModel { ListName.Length: > 0 } model)
                {
                    displaying.Shape.Metadata.Alternates.Add($"{CommerceConstants.Shapes.AdminListFilters}__{model.ListName}");
                }
            });

        return ValueTask.CompletedTask;
    }
}

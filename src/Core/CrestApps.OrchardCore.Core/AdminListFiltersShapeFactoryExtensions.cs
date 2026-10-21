using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Core.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Core;

/// <summary>
/// Creates the filters shape at the end of a commerce admin list's toolbar.
/// </summary>
public static class AdminListFiltersShapeFactoryExtensions
{
    /// <summary>
    /// Creates the <see cref="CommerceConstants.Shapes.AdminListFilters"/> shape, to pass to the list as its
    /// <c>ToolbarActions</c>.
    /// </summary>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="listName">The list's name, which names the shape's alternate.</param>
    /// <param name="formId">The id of the form the filters submit.</param>
    /// <param name="filters">The filters, in the order they're shown.</param>
    public static ValueTask<IShape> CreateAdminListFiltersAsync(this IShapeFactory shapeFactory, string listName, string formId, params AdminListFilter[] filters)
    {
        ArgumentNullException.ThrowIfNull(shapeFactory);
        ArgumentException.ThrowIfNullOrEmpty(formId);

        return shapeFactory.CreateAsync<AdminListFiltersViewModel>(CommerceConstants.Shapes.AdminListFilters, model =>
        {
            model.ListName = listName;
            model.FormId = formId;
            model.Filters = filters ?? [];
        });
    }
}

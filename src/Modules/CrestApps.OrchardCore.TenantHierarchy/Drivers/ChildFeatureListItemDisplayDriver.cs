using CrestApps.OrchardCore.TenantHierarchy.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.TenantHierarchy.Drivers;

/// <summary>
/// Displays a feature of a child tenant as a row of its features list, in the List and Grid layouts of the admin list.
/// </summary>
public sealed class ChildFeatureListItemDisplayDriver : DisplayDriver<ChildFeatureListItem>
{
    public override IDisplayResult Display(ChildFeatureListItem item, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        var feature = item.Feature;

        // The list is searched as you type, on the name, the identifier, the category and the description.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{feature.Name} {feature.Id} {feature.Category} {feature.Description}".ToLowerInvariant();
        }

        return Combine(
            View("ChildFeatureListItem_Fields_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ChildFeatureListItem_Description_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("ChildFeatureListItem_Category_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Category:5"),
            View("ChildFeatureListItem_State_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "State:5"),
            View("ChildFeatureListItem_Buttons_SummaryAdmin", item)
                .Location("Actions:5")
                .RenderWhen(() => Task.FromResult(!feature.IsAlwaysEnabled)));
    }
}

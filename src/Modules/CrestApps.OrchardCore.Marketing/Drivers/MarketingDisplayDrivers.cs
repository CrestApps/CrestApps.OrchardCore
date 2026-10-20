using CrestApps.OrchardCore.Marketing.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Marketing.Drivers;

/// <summary>
/// Renders a segment as a row of the segments admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class SegmentDisplayDriver : DisplayDriver<Segment>
{
    public override IDisplayResult Display(Segment segment, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = segment.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("Segment_Fields_SummaryAdmin", segment)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Segment_Description_SummaryAdmin", segment)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Rules:1"),
            View("Segment_Buttons_SummaryAdmin", segment).Location("Actions:5"));
    }
}

/// <summary>
/// Renders a campaign as a row of the campaigns admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class CampaignDisplayDriver : DisplayDriver<Campaign>
{
    public override IDisplayResult Display(Campaign campaign, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = campaign.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("Campaign_Fields_SummaryAdmin", campaign)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Campaign_Description_SummaryAdmin", campaign)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("Campaign_Status_SummaryAdmin", campaign)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("Campaign_Buttons_SummaryAdmin", campaign).Location("Actions:5"));
    }
}

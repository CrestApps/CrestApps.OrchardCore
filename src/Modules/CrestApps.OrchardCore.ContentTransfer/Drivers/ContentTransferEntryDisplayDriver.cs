using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.ContentTransfer.ViewModels;
using OrchardCore;
using OrchardCore.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContentTransfer.Drivers;

public sealed class ContentTransferEntryDisplayDriver : DisplayDriver<ContentTransferEntry>
{
    public override Task<IDisplayResult> DisplayAsync(ContentTransferEntry entry, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{entry.UploadedFileName} {entry.ContentType}".Trim().ToLowerInvariant();
        }

        return CombineAsync(
            View("ContentTransferEntry_Checkbox_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1"),
            View("ContentTransferEntry_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ContentTransferEntry_ContentType_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Type:5"),
            View("ContentTransferEntry_Status_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("ContentTransferEntriesMeta_SummaryAdmin", new ContentTransferEntryViewModel(entry))
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:20")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Created:5"),
            View("ContentTransferEntry_Records_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:25")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Records:5"),
            View("ContentTransferEntry_Error_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrWhiteSpace(entry.Error))),
            View("ContentTransferEntry_ErrorCategories_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:8")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Records:10")
                .RenderWhen(() => Task.FromResult(entry.TryGet<ImportFileProcessStatsPart>(out var statsPart) && statsPart.ErrorCategories?.Count > 0)),
            View("ContentTransferEntriesProgress_SummaryAdmin", new ContentTransferEntryViewModel(entry))
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:10"),
            View("ContentTransferEntriesActions_SummaryAdmin", new ContentTransferEntryViewModel(entry)).Location("Actions:5"),
            View("ContentTransferEntriesButtonActions_SummaryAdmin", new ContentTransferEntryViewModel(entry)).Location("ActionsMenu:10")
        );
    }
}

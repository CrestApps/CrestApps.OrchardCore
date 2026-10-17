using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

/// <summary>
/// Renders a message's row on the shared voicemail list: who called, its queue and handling, its recording, and what
/// the viewer may do with it.
/// </summary>
internal sealed class SharedVoicemailEntryDisplayDriver : DisplayDriver<SharedVoicemailEntry>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(SharedVoicemailEntry entry, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{entry.Voicemail.CallerName} {entry.Voicemail.CallerNumber}".Trim().ToLowerInvariant();
        }

        return CombineAsync(
            View("SharedVoicemailEntry_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("SharedVoicemailEntry_Description_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrWhiteSpace(entry.Voicemail.ResolutionNote))),
            View("SharedVoicemailEntry_DefaultTags_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("SharedVoicemailEntry_DefaultMeta_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Received:5"),
            View("SharedVoicemailEntry_Recording_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Recording:5"),
            View("SharedVoicemailEntry_Resolve_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Recording:10")
                .RenderWhen(() => Task.FromResult(entry.CanResolve)),
            View("SharedVoicemailEntry_Buttons_SummaryAdmin", entry).Location("Actions:5")
                .RenderWhen(() => Task.FromResult(entry.CanClaim || entry.CanCallBack || entry.CanRelease || entry.CanDelete))
        );
    }
}

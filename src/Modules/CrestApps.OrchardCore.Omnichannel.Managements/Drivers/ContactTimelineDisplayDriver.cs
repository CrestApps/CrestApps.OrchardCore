using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Adds the Notes card to the page of a contact or a lead: its timeline, with a composer for the team's notes.
/// </summary>
internal sealed class ContactTimelineDisplayDriver : ContentDisplayDriver
{
    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<OmnichannelContactPart>() || contentItem.Has<LeadPart>();

    public override IDisplayResult Display(ContentItem contentItem, BuildDisplayContext context)
    {
        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin)
        {
            return null;
        }

        return View("ContactTimelineNotes", contentItem)
            .Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Content:20");
    }
}

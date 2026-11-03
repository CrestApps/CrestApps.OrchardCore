using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Calendar;

/// <summary>
/// Adds "Add to my calendar" to the follow-up activities of the completion page.
/// </summary>
internal sealed class OmnichannelActivityCalendarDisplayDriver : DisplayDriver<OmnichannelActivity>
{
    public override IDisplayResult Edit(OmnichannelActivity activity, BuildEditorContext context)
    {
        if (activity.Status == ActivityStatus.Completed)
        {
            return null;
        }

        return View("OmnichannelActivityCalendar_Edit", activity)
            .Location("Content:9")
            .OnGroup(OmnichannelConstants.CompleteActivityGroup);
    }
}

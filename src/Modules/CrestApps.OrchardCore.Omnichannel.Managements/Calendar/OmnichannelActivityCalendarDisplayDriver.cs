using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Calendar;

/// <summary>
/// Adds "Add to my calendar" to the follow-up activities of the completion page.
/// </summary>
internal sealed class OmnichannelActivityCalendarDisplayDriver : DisplayDriver<OmnichannelActivity>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityCalendarDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelActivityCalendarDisplayDriver(IStringLocalizer<OmnichannelActivityCalendarDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(OmnichannelActivity activity, BuildEditorContext context)
    {
        if (activity.Status == ActivityStatus.Completed)
        {
            return null;
        }

        return View("OmnichannelActivityCalendar_Edit", activity)
            .Location(l => l.Zone("Content", "9").Card(S["Activity Information"], "20"))
            .OnGroup(OmnichannelConstants.CompleteActivityGroup);
    }
}

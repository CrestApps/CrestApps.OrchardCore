using CrestApps.OrchardCore.Scheduling.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Classes.Services;

/// <summary>
/// The choices the class and session editors share.
/// </summary>
internal static class ClassesSelectLists
{
    /// <summary>
    /// Lists what a full session does when a place frees up.
    /// </summary>
    /// <param name="S">The string localizer.</param>
    /// <param name="selected">The selected mode.</param>
    /// <returns>The choices.</returns>
    public static List<SelectListItem> WaitlistModes(IStringLocalizer S, SchedulingWaitlistMode selected) =>
    [
        new SelectListItem(S["Offer the place to the next person, held for a while"], nameof(SchedulingWaitlistMode.AutoPromote), selected == SchedulingWaitlistMode.AutoPromote),
        new SelectListItem(S["Tell the next person, who registers themselves"], nameof(SchedulingWaitlistMode.Notify), selected == SchedulingWaitlistMode.Notify),
        new SelectListItem(S["No waitlist: a full session refuses"], nameof(SchedulingWaitlistMode.None), selected == SchedulingWaitlistMode.None),
    ];
}

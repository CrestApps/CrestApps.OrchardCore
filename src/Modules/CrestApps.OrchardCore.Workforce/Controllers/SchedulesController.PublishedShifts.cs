using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// Who may change a shift on the published schedule.
/// </summary>
public sealed partial class SchedulesController
{
    // A manager never changes their own shift, nor gives one to themselves, on the strength of managing their own team;
    // and a shift that started or ended is changed only with its own permission, so a day's adherence can't be rewritten
    // before it is saved. Returns the refusal, or null when the change may go ahead.
    private async Task<IActionResult> RefusePublishedShiftChangeAsync(CalendarEvent shift, ShiftData data, string newWorkerId, IClock clock, string periodId, string id)
    {
        if ((!string.IsNullOrEmpty(data.WorkerId) && !await _scopeService.CanDecideForWorkerAsync(User, data.WorkerId, HttpContext.RequestAborted)) ||
            (!string.IsNullOrEmpty(newWorkerId) && !await _scopeService.CanDecideForWorkerAsync(User, newWorkerId, HttpContext.RequestAborted)))
        {
            return Forbid();
        }

        if (shift.StartUtc > clock.UtcNow || await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ChangeStartedShifts))
        {
            return null;
        }

        await _notifier.ErrorAsync(H["This shift has started or ended. Changing it needs the permission to change shifts that have started or ended."]);

        return RedirectToAction(nameof(Shift), new { periodId, id });
    }
}

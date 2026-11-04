using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Adds "Shift schedules" to the actions of a team's Workforce calendar on Manage Calendars.
/// </summary>
internal sealed class WorkforceCalendarRowDisplayDriver : DisplayDriver<Calendar>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public WorkforceCalendarRowDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override async Task<IDisplayResult> DisplayAsync(Calendar calendar, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid)
            || calendar.Type != WorkforceConstants.CalendarType
            || calendar.Source != WorkforceConstants.Source
            || string.IsNullOrEmpty(calendar.SourceId)
            || _httpContextAccessor.HttpContext?.User is not { } user
            || !await _authorizationService.AuthorizeAsync(user, WorkforcePermissions.ManageSchedules))
        {
            return null;
        }

        return View("Calendar_ShiftSchedules_SummaryAdmin", calendar).Location("ActionsMenu:5");
    }
}

using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Calendars.Drivers;

/// <summary>
/// Adds the feed, export and import actions to the rows of the calendar list.
/// </summary>
internal sealed class CalendarFeedsDisplayDriver : DisplayDriver<Calendar>
{
    private readonly ICalendarAccessService _accessService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CalendarFeedsDisplayDriver(
        ICalendarAccessService accessService,
        IHttpContextAccessor httpContextAccessor)
    {
        _accessService = accessService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(Calendar calendar, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        var level = await _accessService.GetAccessLevelAsync(_httpContextAccessor.HttpContext?.User, calendar);

        return Initialize<CalendarRowViewModel>("Calendar_FeedActions_SummaryAdmin", model =>
        {
            model.Calendar = calendar;
            model.Level = level;
        }).Location("ActionsMenu:5");
    }
}

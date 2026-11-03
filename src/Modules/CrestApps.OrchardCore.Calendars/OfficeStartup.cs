using CrestApps.OrchardCore.Calendars.Drivers;
using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registers the Office calendar type.
/// </summary>
[Feature(CalendarsConstants.Features.Office)]
public sealed class OfficeStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<Calendar, OfficeCalendarSettingsDisplayDriver>();

        services.AddCalendarType(CalendarsConstants.Types.Office, type => type
            .WithDisplayName(LocalizationSource.Create<OfficeStartup>("Office"))
            .WithDescription(LocalizationSource.Create<OfficeStartup>("Who is in an office and which desks are booked, up to the number of desks."))
            .WithIcon("fa-solid fa-building")
            .WithColor("#e67700")
            .WithVisibility(CalendarVisibility.Public)
            .LimitConcurrent(20)
            .WithPosition(40)
            .AddEventType("InOffice", LocalizationSource.Create<OfficeStartup>("In office"), e => e.Color("#e67700").AllDayByDefault().RequiresAttendee())
            .AddEventType("DeskBooking", LocalizationSource.Create<OfficeStartup>("Desk booking"), e => e.Color("#f08c00").RequiresAttendee().Lasts(240))
            .AddEventType("Visitor", LocalizationSource.Create<OfficeStartup>("Visitor"), e => e.Color("#1098ad"))
            .AddEventType("Closed", LocalizationSource.Create<OfficeStartup>("Office closed"), e => e.Color("#495057").AllDayByDefault().Free()));
    }
}

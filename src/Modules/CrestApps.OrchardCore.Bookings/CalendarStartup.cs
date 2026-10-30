using CrestApps.OrchardCore.Bookings.Calendar;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Bookings.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Bookings;

/// <summary>
/// Keeps every booking on a Bookings calendar.
/// </summary>
[Feature(BookingsConstants.CalendarFeature)]
public sealed class CalendarStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<BookingCalendarHandler>();
        services.AddScoped<IBookingHandler>(serviceProvider => serviceProvider.GetRequiredService<BookingCalendarHandler>());

        // A product can be blocked with a calendar, such as a room's, so what's busy there can't be booked.
        services.AddScoped<IBookingSlotFilter, BookingCalendarSlotFilter>();
        services.AddContentPart<BookingPart>()
            .UseDisplayDriver<BookingPartCalendarDisplayDriver>();

        services.AddCalendarType(BookingCalendarHandler.CalendarType, type => type
            .WithDisplayName(LocalizationSource.Create<CalendarStartup>("Bookings"))
            .WithDescription(LocalizationSource.Create<CalendarStartup>("The store's bookings. Bookings creates this calendar and keeps it up to date."))
            .WithIcon("fa-solid fa-ticket")
            .WithColor("#c2255c")
            .WithPosition(60)
            .ManagedByCode()
            .AddEventType(BookingCalendarHandler.EventType, LocalizationSource.Create<CalendarStartup>("Booking"), e => e.Color("#c2255c")));
    }
}

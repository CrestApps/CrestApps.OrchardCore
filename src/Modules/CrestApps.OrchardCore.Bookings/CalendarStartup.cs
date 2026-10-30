using CrestApps.OrchardCore.Bookings.Calendar;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Calendars;
using Microsoft.Extensions.DependencyInjection;
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

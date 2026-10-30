using CrestApps.OrchardCore.Calendars.Drivers;
using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registers the Room and Equipment calendar types.
/// </summary>
[Feature(CalendarsConstants.Features.Resources)]
public sealed class ResourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<Calendar, ResourceCalendarSettingsDisplayDriver>();

        services.AddCalendarType(CalendarsConstants.Types.Room, type => type
            .WithDisplayName(LocalizationSource.Create<ResourcesStartup>("Room"))
            .WithDescription(LocalizationSource.Create<ResourcesStartup>("A meeting room or another space one group uses at a time. Invite it to an event to book it."))
            .WithIcon("fa-solid fa-door-open")
            .WithColor("#5f3dc4")
            .WithOwner(CalendarOwnerKind.Resource)
            .WithVisibility(CalendarVisibility.Public)
            .LimitConcurrent(1)
            .WithPosition(20)
            .AddEventType("Booking", LocalizationSource.Create<ResourcesStartup>("Booking"), e => e.Color("#5f3dc4"))
            .AddEventType("Maintenance", LocalizationSource.Create<ResourcesStartup>("Maintenance"), e => e.Color("#868e96")));

        services.AddCalendarType(CalendarsConstants.Types.Equipment, type => type
            .WithDisplayName(LocalizationSource.Create<ResourcesStartup>("Equipment"))
            .WithDescription(LocalizationSource.Create<ResourcesStartup>("A piece of equipment that can be reserved, such as a projector or a vehicle."))
            .WithIcon("fa-solid fa-toolbox")
            .WithColor("#9c36b5")
            .WithOwner(CalendarOwnerKind.Resource)
            .WithVisibility(CalendarVisibility.Public)
            .LimitConcurrent(1)
            .WithPosition(21)
            .AddEventType("Reservation", LocalizationSource.Create<ResourcesStartup>("Reservation"), e => e.Color("#9c36b5"))
            .AddEventType("Maintenance", LocalizationSource.Create<ResourcesStartup>("Maintenance"), e => e.Color("#868e96")));
    }
}

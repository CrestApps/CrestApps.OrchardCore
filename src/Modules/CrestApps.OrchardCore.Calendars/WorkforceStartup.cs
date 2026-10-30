using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registers the Workforce calendar type.
/// </summary>
[Feature(CalendarsConstants.Features.Workforce)]
public sealed class WorkforceStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCalendarType(CalendarsConstants.Types.Workforce, type => type
            .WithDisplayName(LocalizationSource.Create<WorkforceStartup>("Workforce"))
            .WithDescription(LocalizationSource.Create<WorkforceStartup>("Shifts, time off and training for a group of employees. The employees on a shift are its attendees."))
            .WithIcon("fa-solid fa-user-clock")
            .WithColor("#2b8a3e")
            .WithOwner(CalendarOwnerKind.Role)
            .WithPosition(30)
            .AddEventType("Shift", LocalizationSource.Create<WorkforceStartup>("Shift"), e => e.Color("#2b8a3e").RequiresAttendee().Lasts(480))
            .AddEventType("TimeOff", LocalizationSource.Create<WorkforceStartup>("Time off"), e => e.Color("#e8590c").AllDayByDefault().NeedsApproval().RequiresAttendee())
            .AddEventType("Break", LocalizationSource.Create<WorkforceStartup>("Break"), e => e.Color("#868e96").Lasts(30).RequiresAttendee())
            .AddEventType("Training", LocalizationSource.Create<WorkforceStartup>("Training"), e => e.Color("#1971c2").Lasts(120)));
    }
}

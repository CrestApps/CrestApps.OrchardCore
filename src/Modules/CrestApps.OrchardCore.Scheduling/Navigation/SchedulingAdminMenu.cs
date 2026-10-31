using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Scheduling.Navigation;

/// <summary>
/// Adds the Scheduling items to the Calendars menu.
/// </summary>
public sealed class SchedulingAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulingAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SchedulingAdminMenu(IStringLocalizer<SchedulingAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Calendars"], calendars => calendars
            .Add(S["Appointments"], S["Appointments"].PrefixPosition("4"), appointments => appointments
                .Id("appointments")
                .Action("Index", "Appointments", SchedulingConstants.Features.Scheduling)
                .Permission(SchedulingPermissions.ViewAppointments)
                .LocalNav())
            .Add(S["Appointment Types"], S["Appointment Types"].PrefixPosition("5"), types => types
                .Id("appointmentTypes")
                .Action("Index", "AppointmentTypes", SchedulingConstants.Features.Scheduling)
                .Permission(SchedulingPermissions.ManageScheduling)
                .LocalNav())
            .Add(S["Scheduling Resources"], S["Scheduling Resources"].PrefixPosition("6"), resources => resources
                .Id("schedulingResources")
                .Action("Index", "SchedulingResources", SchedulingConstants.Features.Scheduling)
                .Permission(SchedulingPermissions.ManageScheduling)
                .LocalNav())
            .Add(S["Waitlist"], S["Waitlist"].PrefixPosition("7"), waitlist => waitlist
                .Id("schedulingWaitlist")
                .Action("Index", "Waitlist", SchedulingConstants.Features.Scheduling)
                .Permission(SchedulingPermissions.ManageAppointments)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

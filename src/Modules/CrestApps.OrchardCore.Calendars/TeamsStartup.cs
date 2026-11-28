using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Navigation;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Teams;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registers the Team calendar type.
/// </summary>
[Feature(CalendarsConstants.Features.Teams)]
public sealed class TeamsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<TeamCalendarsAdminMenu>();
        services.AddScoped<TeamAvailabilityService>();

        services.AddCalendarType(CalendarsConstants.Types.Team, type => type
            .WithDisplayName(LocalizationSource.Create<TeamsStartup>("Team"))
            .WithDescription(LocalizationSource.Create<TeamsStartup>("A team's calendar. Everyone in the team's role, and the members of the team linked to that role, can see it and add to it."))
            .WithIcon("fa-solid fa-people-group")
            .WithColor("#0c8599")
            .WithOwner(CalendarOwnerKind.Role)
            .WithPosition(10)
            .AddEventType("Meeting", LocalizationSource.Create<TeamsStartup>("Meeting"), e => e.Color("#0c8599"))
            .AddEventType("OnCall", LocalizationSource.Create<TeamsStartup>("On call"), e => e.Color("#d6336c").RequiresAttendee().Lasts(480))
            .AddEventType(CalendarsConstants.EventTypes.OutOfOffice, LocalizationSource.Create<TeamsStartup>("Out of office"), e => e.Color("#e8590c").AllDayByDefault().RequiresAttendee().IgnoresCapacity().MarksAbsence())
            .AddEventType("Milestone", LocalizationSource.Create<TeamsStartup>("Milestone"), e => e.Color("#5c940d").AllDayByDefault().Free()));
    }
}

/// <summary>
/// Lets the members of a team (from the shared team membership) reach the team calendar of the team's linked role, as
/// well as the users in the role.
/// </summary>
[Feature(CalendarsConstants.Features.Teams)]
[RequireFeatures(TeamsConstants.Features.Teams)]
public sealed class TeamMembershipStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCalendarAccessContributor<TeamMembershipCalendarAccessContributor>();
    }
}

/// <summary>
/// Tells team assignment who is away now because of an absence on a calendar (out of office, time off).
/// </summary>
[RequireFeatures(TeamsConstants.Features.Teams)]
public sealed class TeamAvailabilityStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTeamMemberAvailabilitySource<CalendarAbsenceAvailabilitySource>();
    }
}

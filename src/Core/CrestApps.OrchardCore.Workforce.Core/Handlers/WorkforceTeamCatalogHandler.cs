using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Workforce.Core.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Handlers;

/// <summary>
/// Gives each team its Workforce calendar when it is saved, and keeps the calendar's name, time zone and status in step
/// with the team.
/// </summary>
internal sealed class WorkforceTeamCatalogHandler : CatalogEntryHandlerBase<Team>
{
    private readonly WorkforceCalendarService _calendarService;

    public WorkforceTeamCatalogHandler(WorkforceCalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    public override Task CreatingAsync(CreatingContext<Team> context, CancellationToken cancellationToken = default)
    {
        return _calendarService.EnsureCalendarAsync(context.Model, cancellationToken);
    }

    public override Task UpdatingAsync(UpdatingContext<Team> context, CancellationToken cancellationToken = default)
    {
        return _calendarService.EnsureCalendarAsync(context.Model, cancellationToken);
    }
}

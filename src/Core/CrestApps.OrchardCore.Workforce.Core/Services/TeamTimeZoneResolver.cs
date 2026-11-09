using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Teams.Core.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Works out a team's time zone: the team's own, else the nearest parent team's, else the site's. Dispatch walks the same
/// chain, so a worker's days fall in the same zone in Workforce and Dispatch.
/// </summary>
internal sealed class TeamTimeZoneResolver
{
    private readonly TeamStore _teamStore;
    private readonly ISiteService _siteService;

    public TeamTimeZoneResolver(
        TeamStore teamStore,
        ISiteService siteService)
    {
        _teamStore = teamStore;
        _siteService = siteService;
    }

    public async Task<string> GetAsync(string teamId, CancellationToken cancellationToken = default)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);

        for (var id = teamId; !string.IsNullOrEmpty(id) && visited.Add(id);)
        {
            var team = await _teamStore.FindByIdAsync(id, cancellationToken);

            if (team is null)
            {
                break;
            }

            if (CalendarTimeZones.IsValid(team.TimeZoneId))
            {
                return team.TimeZoneId;
            }

            id = team.ParentId;
        }

        var timeZoneId = (await _siteService.GetSiteSettingsAsync()).TimeZoneId;

        return string.IsNullOrEmpty(timeZoneId) ? "UTC" : timeZoneId;
    }
}

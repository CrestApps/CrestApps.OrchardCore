using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Works out a team's time zone: the team's own, or the site's.
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
        var team = string.IsNullOrEmpty(teamId) ? null : await _teamStore.FindByIdAsync(teamId, cancellationToken);

        if (!string.IsNullOrEmpty(team?.TimeZoneId))
        {
            return team.TimeZoneId;
        }

        var timeZoneId = (await _siteService.GetSiteSettingsAsync()).TimeZoneId;

        return string.IsNullOrEmpty(timeZoneId) ? "UTC" : timeZoneId;
    }
}

using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Geo;

/// <summary>
/// The kinds of Workforce records that are geocoded.
/// </summary>
public static class WorkforceGeocodeTargets
{
    /// <summary>
    /// A team's location.
    /// </summary>
    public const string TeamLocation = "WorkforceTeamLocation";

    /// <summary>
    /// Tells whether a location has an address and no coordinates yet.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns><see langword="true"/> when it should be geocoded.</returns>
    public static bool NeedsGeocoding(WorkforceLocation location)
        => !string.IsNullOrWhiteSpace(location?.Address) && (location.Latitude is null || location.Longitude is null);
}

/// <summary>
/// Asks for a team's location to be geocoded when it has an address and no coordinates.
/// </summary>
internal sealed class TeamLocationGeocodeHandler : CatalogEntryHandlerBase<Team>
{
    private readonly Lazy<IGeocodeQueue> _queue;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamLocationGeocodeHandler"/> class.
    /// </summary>
    /// <param name="queue">The geocoding queue.</param>
    public TeamLocationGeocodeHandler(Lazy<IGeocodeQueue> queue)
    {
        _queue = queue;
    }

    /// <inheritdoc/>
    public override Task CreatedAsync(CreatedContext<Team> context, CancellationToken cancellationToken = default)
        => EnqueueAsync(context.Model, cancellationToken);

    /// <inheritdoc/>
    public override Task UpdatedAsync(UpdatedContext<Team> context, CancellationToken cancellationToken = default)
        => EnqueueAsync(context.Model, cancellationToken);

    private Task EnqueueAsync(Team team, CancellationToken cancellationToken)
        => team.GetWorkforceSettings().Location is var location && WorkforceGeocodeTargets.NeedsGeocoding(location)
            ? _queue.Value.EnqueueAsync(WorkforceGeocodeTargets.TeamLocation, team.ItemId, new GeocodeQuery { Text = location.Address }, cancellationToken)
            : Task.CompletedTask;
}

/// <summary>
/// Writes geocoded coordinates to a team's location.
/// </summary>
internal sealed class TeamLocationGeocodeTarget : IGeocodeTarget
{
    private readonly ITeamManager _teamManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamLocationGeocodeTarget"/> class.
    /// </summary>
    /// <param name="teamManager">The team manager.</param>
    public TeamLocationGeocodeTarget(ITeamManager teamManager)
    {
        _teamManager = teamManager;
    }

    /// <inheritdoc/>
    public string TargetType => WorkforceGeocodeTargets.TeamLocation;

    /// <inheritdoc/>
    public async Task<bool> ApplyAsync(string targetId, GeocodeCandidate candidate, string source, CancellationToken cancellationToken = default)
    {
        var team = await _teamManager.FindByIdAsync(targetId, cancellationToken);

        var location = team?.GetWorkforceSettings().Location;

        if (location is null || string.IsNullOrWhiteSpace(location.Address))
        {
            return false;
        }

        location.Latitude = candidate.Point.Latitude;
        location.Longitude = candidate.Point.Longitude;
        await _teamManager.UpdateAsync(team, null, cancellationToken);

        return true;
    }
}


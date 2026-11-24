using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;
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
    public static bool NeedsGeocoding(Team team)
        => AddressText(team?.Address) is not null
            && team.GetWorkforceSettings().Location is not { Latitude: not null, Longitude: not null };

    /// <summary>
    /// Writes a team's address on one line, for the geocoder and the Workforce card.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>The line, or <see langword="null"/> when there is no address.</returns>
    public static string AddressText(Address address)
    {
        if (address is null)
        {
            return null;
        }

        var parts = new[] { address.AddressLine1, address.AddressLine2, address.City, address.Region, address.PostalCode, address.Country }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.Trim())
            .ToArray();

        return parts.Length == 0 ? null : string.Join(", ", parts);
    }
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
        => WorkforceGeocodeTargets.NeedsGeocoding(team)
            ? _queue.Value.EnqueueAsync(WorkforceGeocodeTargets.TeamLocation, team.ItemId, new GeocodeQuery { Text = WorkforceGeocodeTargets.AddressText(team.Address) }, cancellationToken)
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

        // A point set by hand in the meantime wins over the geocoder's.
        if (!WorkforceGeocodeTargets.NeedsGeocoding(team))
        {
            return false;
        }

        team.GetWorkforceSettings().Location = new WorkforceLocation { Latitude = candidate.Point.Latitude, Longitude = candidate.Point.Longitude };
        await _teamManager.UpdateAsync(team, null, cancellationToken);

        return true;
    }
}


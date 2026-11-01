using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
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
    /// A worker's home base.
    /// </summary>
    public const string WorkerHomeBase = "WorkforceWorkerHomeBase";

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
internal sealed class TeamLocationGeocodeHandler : CatalogEntryHandlerBase<WorkforceTeam>
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
    public override Task CreatedAsync(CreatedContext<WorkforceTeam> context, CancellationToken cancellationToken = default)
        => EnqueueAsync(context.Model, cancellationToken);

    /// <inheritdoc/>
    public override Task UpdatedAsync(UpdatedContext<WorkforceTeam> context, CancellationToken cancellationToken = default)
        => EnqueueAsync(context.Model, cancellationToken);

    private Task EnqueueAsync(WorkforceTeam team, CancellationToken cancellationToken)
        => WorkforceGeocodeTargets.NeedsGeocoding(team.Location)
            ? _queue.Value.EnqueueAsync(WorkforceGeocodeTargets.TeamLocation, team.ItemId, new GeocodeQuery { Text = team.Location.Address }, cancellationToken)
            : Task.CompletedTask;
}

/// <summary>
/// Asks for a worker's home base to be geocoded when it has an address and no coordinates.
/// </summary>
internal sealed class WorkerHomeBaseGeocodeHandler : CatalogEntryHandlerBase<WorkerProfile>
{
    private readonly Lazy<IGeocodeQueue> _queue;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerHomeBaseGeocodeHandler"/> class.
    /// </summary>
    /// <param name="queue">The geocoding queue.</param>
    public WorkerHomeBaseGeocodeHandler(Lazy<IGeocodeQueue> queue)
    {
        _queue = queue;
    }

    /// <inheritdoc/>
    public override Task CreatedAsync(CreatedContext<WorkerProfile> context, CancellationToken cancellationToken = default)
        => EnqueueAsync(context.Model, cancellationToken);

    /// <inheritdoc/>
    public override Task UpdatedAsync(UpdatedContext<WorkerProfile> context, CancellationToken cancellationToken = default)
        => EnqueueAsync(context.Model, cancellationToken);

    private Task EnqueueAsync(WorkerProfile worker, CancellationToken cancellationToken)
        => WorkforceGeocodeTargets.NeedsGeocoding(worker.HomeBase)
            ? _queue.Value.EnqueueAsync(WorkforceGeocodeTargets.WorkerHomeBase, worker.ItemId, new GeocodeQuery { Text = worker.HomeBase.Address }, cancellationToken)
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

        if (team?.Location is null || string.IsNullOrWhiteSpace(team.Location.Address))
        {
            return false;
        }

        team.Location.Latitude = candidate.Point.Latitude;
        team.Location.Longitude = candidate.Point.Longitude;
        await _teamManager.UpdateAsync(team, null, cancellationToken);

        return true;
    }
}

/// <summary>
/// Writes geocoded coordinates to a worker's home base.
/// </summary>
internal sealed class WorkerHomeBaseGeocodeTarget : IGeocodeTarget
{
    private readonly IWorkerManager _workerManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerHomeBaseGeocodeTarget"/> class.
    /// </summary>
    /// <param name="workerManager">The worker manager.</param>
    public WorkerHomeBaseGeocodeTarget(IWorkerManager workerManager)
    {
        _workerManager = workerManager;
    }

    /// <inheritdoc/>
    public string TargetType => WorkforceGeocodeTargets.WorkerHomeBase;

    /// <inheritdoc/>
    public async Task<bool> ApplyAsync(string targetId, GeocodeCandidate candidate, string source, CancellationToken cancellationToken = default)
    {
        var worker = await _workerManager.FindByIdAsync(targetId, cancellationToken);

        if (worker?.HomeBase is null || string.IsNullOrWhiteSpace(worker.HomeBase.Address))
        {
            return false;
        }

        worker.HomeBase.Latitude = candidate.Point.Latitude;
        worker.HomeBase.Longitude = candidate.Point.Longitude;
        await _workerManager.UpdateAsync(worker, null, cancellationToken);

        return true;
    }
}

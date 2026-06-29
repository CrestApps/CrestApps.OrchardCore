using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IActivityReservationManager"/>.
/// </summary>
public sealed class ActivityReservationManager : CatalogManager<ActivityReservation>, IActivityReservationManager
{
    private readonly IActivityReservationStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityReservationManager"/> class.
    /// </summary>
    /// <param name="store">The underlying reservation store.</param>
    /// <param name="handlers">The catalog entry handlers for reservations.</param>
    /// <param name="logger">The logger instance.</param>
    public ActivityReservationManager(
        IActivityReservationStore store,
        IEnumerable<ICatalogEntryHandler<ActivityReservation>> handlers,
        ILogger<CatalogManager<ActivityReservation>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ActivityReservation>> ListExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var reservations = await _store.ListExpiredAsync(utcNow, cancellationToken);

        foreach (var reservation in reservations)
        {
            await LoadAsync(reservation, cancellationToken);
        }

        return reservations;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> FindPendingByAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        var reservation = await _store.FindPendingByAgentAsync(agentId, cancellationToken);

        if (reservation is not null)
        {
            await LoadAsync(reservation, cancellationToken);
        }

        return reservation;
    }
}

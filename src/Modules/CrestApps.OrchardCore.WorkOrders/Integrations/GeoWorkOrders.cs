using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;

namespace CrestApps.OrchardCore.WorkOrders.Integrations;

/// <summary>
/// Gives a new work order the territory of its service address, and asks for the address to be geocoded when it has no
/// coordinates.
/// </summary>
internal sealed class GeoWorkOrderHandler : IWorkOrderHandler
{
    /// <summary>
    /// The kind of record geocoded.
    /// </summary>
    public const string TargetType = "WorkOrderServiceAddress";

    private readonly ITerritoryResolver _territories;
    private readonly Lazy<IGeocodeQueue> _queue;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoWorkOrderHandler"/> class.
    /// </summary>
    /// <param name="territories">The territory resolver.</param>
    /// <param name="queue">The geocoding queue.</param>
    public GeoWorkOrderHandler(ITerritoryResolver territories, Lazy<IGeocodeQueue> queue)
    {
        _territories = territories;
        _queue = queue;
    }

    /// <inheritdoc/>
    public Task CreatingAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
        => LocateAsync(workOrder, keepTerritory: true, cancellationToken);

    /// <inheritdoc/>
    public Task UpdatingAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
        => workOrder.ServiceAddress is { Latitude: null } ? LocateAsync(workOrder, keepTerritory: false, cancellationToken) : Task.CompletedTask;

    private async Task LocateAsync(WorkOrder workOrder, bool keepTerritory, CancellationToken cancellationToken)
    {
        var address = workOrder.ServiceAddress;

        if (address is null)
        {
            return;
        }

        var match = await _territories.ResolveAsync(GeoPoint.From(address.Latitude, address.Longitude), address.PostalCode, cancellationToken);

        workOrder.TerritoryId = keepTerritory ? workOrder.TerritoryId ?? match?.Territory.ItemId : match?.Territory.ItemId;

        if (address.Latitude is null && !string.IsNullOrWhiteSpace(address.AddressLine1))
        {
            await _queue.Value.EnqueueAsync(TargetType, workOrder.ItemId, new GeocodeQuery { Address = address }, cancellationToken);
        }
    }
}

/// <summary>
/// Writes geocoded coordinates to a work order's service address, and its territory once it has a point.
/// </summary>
internal sealed class WorkOrderAddressGeocodeTarget : IGeocodeTarget
{
    private readonly WorkOrderStore _store;
    private readonly ITerritoryResolver _territories;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderAddressGeocodeTarget"/> class.
    /// </summary>
    /// <param name="store">The work order store.</param>
    /// <param name="territories">The territory resolver.</param>
    public WorkOrderAddressGeocodeTarget(WorkOrderStore store, ITerritoryResolver territories)
    {
        _store = store;
        _territories = territories;
    }

    /// <inheritdoc/>
    public string TargetType => GeoWorkOrderHandler.TargetType;

    /// <inheritdoc/>
    public async Task<bool> ApplyAsync(string targetId, GeocodeCandidate candidate, string source, CancellationToken cancellationToken = default)
    {
        var workOrder = await _store.FindByIdAsync(targetId, cancellationToken);

        if (workOrder?.ServiceAddress is not { } address)
        {
            return false;
        }

        var located = address;

        workOrder.ServiceAddress = new Address
        {
            Name = located.Name,
            Company = located.Company,
            AddressLine1 = located.AddressLine1,
            AddressLine2 = located.AddressLine2,
            DependentLocality = located.DependentLocality,
            SortingCode = located.SortingCode,
            Country = located.Country,
            Region = located.Region,
            County = located.County,
            City = located.City,
            District = located.District,
            PostalCode = located.PostalCode,
            Phone = located.Phone,
            Latitude = candidate.Point.Latitude,
            Longitude = candidate.Point.Longitude,
            GeocodedUtc = DateTime.UtcNow,
            GeocodeSource = source,
        };

        if (await _territories.ResolveAsync(candidate.Point, address.PostalCode, cancellationToken) is { } match)
        {
            workOrder.TerritoryId = match.Territory.ItemId;
        }

        await _store.UpdateAsync(workOrder, cancellationToken);

        return true;
    }
}

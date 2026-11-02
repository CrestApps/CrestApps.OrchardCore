using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using OrchardCore.Modules;
using Asset = CrestApps.OrchardCore.Assets.Models.Asset;

namespace CrestApps.OrchardCore.Assets.Fleet;

/// <summary>
/// Marks that the Fleet feature is on, so the asset editor shows the fleet section.
/// </summary>
public sealed class FleetFeature;

/// <summary>
/// Gives a schedulable asset its own Equipment calendar, which refuses overlapping bookings, and keeps its name.
/// </summary>
internal sealed class FleetCalendarHandler : CatalogEntryHandlerBase<Asset>
{
    private readonly ICalendarService _calendarService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetCalendarHandler"/> class.
    /// </summary>
    /// <param name="calendarService">The calendar service.</param>
    public FleetCalendarHandler(ICalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    /// <inheritdoc/>
    public override Task CreatingAsync(CreatingContext<Asset> context, CancellationToken cancellationToken = default)
        => EnsureAsync(context.Model, cancellationToken);

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<Asset> context, CancellationToken cancellationToken = default)
        => EnsureAsync(context.Model, cancellationToken);

    private async Task EnsureAsync(Asset asset, CancellationToken cancellationToken)
    {
        if (asset.Fleet is not { Schedulable: true })
        {
            return;
        }

        var options = new CalendarChangeOptions { Actor = AssetsConstants.Source };
        var calendar = (string.IsNullOrEmpty(asset.Fleet.CalendarId) ? null : await _calendarService.FindByIdAsync(asset.Fleet.CalendarId, cancellationToken))
            ?? await _calendarService.FindBySourceAsync(AssetsConstants.Source, asset.ItemId, cancellationToken);

        if (calendar is null)
        {
            calendar = await _calendarService.NewAsync(AssetsConstants.EquipmentCalendarType, cancellationToken);

            if (calendar is null)
            {
                return;
            }

            calendar.Name = asset.Name;
            calendar.Source = AssetsConstants.Source;
            calendar.SourceId = asset.ItemId;

            var created = await _calendarService.CreateAsync(calendar, options, cancellationToken);

            asset.Fleet.CalendarId = created.Succeeded ? created.Value.ItemId : null;

            return;
        }

        asset.Fleet.CalendarId = calendar.ItemId;

        if (calendar.Name != asset.Name)
        {
            calendar.Name = asset.Name;
            await _calendarService.UpdateAsync(calendar, options, cancellationToken);
        }
    }
}

/// <summary>
/// Gives an asset that carries stock its own Inventory location, owned by the asset, and keeps its name.
/// </summary>
internal sealed class FleetStockLocationHandler : CatalogEntryHandlerBase<Asset>
{
    private readonly StockLocationStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetStockLocationHandler"/> class.
    /// </summary>
    /// <param name="store">The stock location store.</param>
    /// <param name="clock">The clock.</param>
    public FleetStockLocationHandler(StockLocationStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    /// <inheritdoc/>
    public override Task CreatingAsync(CreatingContext<Asset> context, CancellationToken cancellationToken = default)
        => EnsureAsync(context.Model, cancellationToken);

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<Asset> context, CancellationToken cancellationToken = default)
        => EnsureAsync(context.Model, cancellationToken);

    private async Task EnsureAsync(Asset asset, CancellationToken cancellationToken)
    {
        if (asset.Fleet is not { CarriesStock: true })
        {
            return;
        }

        var location = await _store.FindByOwnerAsync(AssetsConstants.Source, asset.ItemId, cancellationToken);

        if (location is null)
        {
            location = new StockLocation
            {
                ItemId = CrestApps.Core.UniqueId.GenerateId(),
                Name = asset.Name,
                OwnerKind = AssetsConstants.Source,
                OwnerId = asset.ItemId,
                CreatedUtc = _clock.UtcNow,
            };

            await _store.CreateAsync(location, cancellationToken);
        }
        else if (location.Name != asset.Name)
        {
            location.Name = asset.Name;
            await _store.UpdateAsync(location, cancellationToken);
        }

        asset.Fleet.StockLocationId = location.ItemId;
    }
}

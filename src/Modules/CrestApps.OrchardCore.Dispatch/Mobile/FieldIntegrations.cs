using System.Security.Claims;
using CrestApps.OrchardCore.Assets;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Dispatch.Controllers;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Media;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Dispatch.Mobile;

/// <summary>
/// Reads the technician app's settings from the <c>CrestApps:Dispatch:Mobile</c> configuration section.
/// </summary>
internal sealed class DispatchMobileOptionsConfiguration : IConfigureOptions<DispatchMobileOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    public DispatchMobileOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void Configure(DispatchMobileOptions options)
    {
        _shellConfiguration.GetSection(DispatchConstants.MobileConfigurationSection).Bind(options);
    }
}

/// <summary>
/// Keeps the technician app's photos and signatures in the media library.
/// </summary>
internal sealed class MediaFieldFileStore : IFieldFileStore
{
    private readonly IMediaFileStore _mediaFileStore;

    public MediaFieldFileStore(IMediaFileStore mediaFileStore)
    {
        _mediaFileStore = mediaFileStore;
    }

    public Task<string> SaveAsync(string path, Stream content, CancellationToken cancellationToken = default)
        => _mediaFileStore.CreateFileFromStreamAsync(path, content, overwrite: true);
}

/// <summary>
/// Adds the Field page to My Work for workers, with the number of visits still to do today.
/// </summary>
internal sealed class FieldLinkProvider : IMyWorkLinkProvider
{
    private readonly IDispatchService _dispatchService;
    private readonly DispatchTimeZone _timeZone;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public FieldLinkProvider(IDispatchService dispatchService, DispatchTimeZone timeZone, IClock clock, IStringLocalizer<FieldLinkProvider> stringLocalizer)
    {
        _dispatchService = dispatchService;
        _timeZone = timeZone;
        _clock = clock;
        S = stringLocalizer;
    }

    public async Task<IEnumerable<MyWorkLink>> GetLinksAsync(ClaimsPrincipal user, WorkerProfile worker, CancellationToken cancellationToken = default)
    {
        if (worker is null)
        {
            return [];
        }

        var zone = await _timeZone.GetAsync();
        var today = CalendarTime.ToLocal(_clock.UtcNow, zone).Date;
        var visits = await _dispatchService.ListScheduledAsync(CalendarTime.ToUtc(today, zone), CalendarTime.ToUtc(today.AddDays(1), zone), [worker.ItemId], cancellationToken);
        var open = visits.Count(visit => visit.WorkerIds.Contains(worker.ItemId) && Core.VisitStatusRules.IsOpen(visit.Status) && visit.Status != VisitStatus.Unscheduled);

        return [new MyWorkLink("field", S["Field"], "~/Admin/my-work/field", open)];
    }
}

/// <summary>
/// Lists Inventory's stock locations for parts used, with the van a technician drives as their own.
/// </summary>
internal sealed class InventoryFieldStockSource : IFieldStockSource
{
    private readonly IInventoryLocationService _locations;
    private readonly IServiceProvider _serviceProvider;

    public InventoryFieldStockSource(IInventoryLocationService locations, IServiceProvider serviceProvider)
    {
        _locations = locations;
        _serviceProvider = serviceProvider;
    }

    public async Task<IReadOnlyList<FieldStockLocation>> ListAsync(string workerId, CancellationToken cancellationToken = default)
    {
        var assets = _serviceProvider.GetService<AssetManager>();
        var result = new List<FieldStockLocation>();

        foreach (var location in await _locations.GetLocationsAsync())
        {
            var isOwn = false;

            // A van's location belongs to the van; the van's default driver uses it as their own.
            if (assets is not null && location.OwnerKind == AssetsConstants.Source && !string.IsNullOrEmpty(location.OwnerId))
            {
                var asset = await assets.FindByIdAsync(location.OwnerId, cancellationToken);

                isOwn = asset?.Fleet?.DefaultDriverWorkerId == workerId;
            }

            result.Add(new FieldStockLocation { Id = location.LocationId, Name = location.Name, IsOwn = isOwn });
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<string, Dictionary<string, int>>> AvailableAsync(IReadOnlyCollection<string> locationIds, IReadOnlyCollection<string> stockItemIds, CancellationToken cancellationToken = default)
    {
        var result = locationIds.Distinct(StringComparer.Ordinal).ToDictionary(id => id, _ => new Dictionary<string, int>(StringComparer.Ordinal), StringComparer.Ordinal);

        if (_serviceProvider.GetService<IStockItemStore>() is not { } items)
        {
            return result;
        }

        foreach (var stockItemId in stockItemIds.Distinct(StringComparer.Ordinal))
        {
            var item = await items.FindByIdAsync(stockItemId, cancellationToken);

            if (item is null)
            {
                continue;
            }

            // A location with no level of the item has none of it.
            foreach (var (locationId, units) in result)
            {
                units[stockItemId] = Math.Max(0, item.Levels.FirstOrDefault(level => level.LocationId == locationId)?.Available ?? 0);
            }
        }

        return result;
    }
}

/// <summary>
/// Describes the assets of the technician's work orders, with Assets on.
/// </summary>
internal sealed class AssetsFieldAssetSource : IFieldAssetSource
{
    private readonly AssetManager _assets;

    public AssetsFieldAssetSource(AssetManager assets)
    {
        _assets = assets;
    }

    public async Task<IReadOnlyList<FieldAsset>> GetAsync(IReadOnlyCollection<string> assetIds, CancellationToken cancellationToken = default)
    {
        var result = new List<FieldAsset>();

        foreach (var id in assetIds.Distinct(StringComparer.Ordinal))
        {
            if (await _assets.FindByIdAsync(id, cancellationToken) is { } asset)
            {
                result.Add(new FieldAsset
                {
                    Id = asset.ItemId,
                    Number = asset.Number,
                    Name = asset.Name,
                    SerialNumber = asset.SerialNumber,
                    Model = string.Join(' ', new[] { asset.Manufacturer, asset.Model }.Where(part => !string.IsNullOrWhiteSpace(part))),
                });
            }
        }

        return result;
    }
}

/// <summary>
/// Removes technicians' positions and the records of their commands once they are kept long enough.
/// </summary>
[BackgroundTask(Title = "Dispatch: location retention", Schedule = "15 * * * *", Description = "Removes technicians' positions and the records of their app's commands after their retention.")]
internal sealed class FieldRetentionBackgroundTask : IBackgroundTask
{
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IWorkerLocationService>().PruneAsync(cancellationToken);
}

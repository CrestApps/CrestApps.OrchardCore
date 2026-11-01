using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using YesSql;
using Asset = CrestApps.OrchardCore.Assets.Models.Asset;

namespace CrestApps.OrchardCore.Assets.Core.Services;

/// <summary>
/// Meter readings and the asset hierarchy.
/// </summary>
public sealed class AssetService
{
    private readonly ISession _session;
    private readonly AssetStore _assetStore;
    private readonly AssetTypeStore _typeStore;
    private readonly AssetMeterReadingStore _readingStore;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetService"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="assetStore">The asset store.</param>
    /// <param name="typeStore">The asset type store.</param>
    /// <param name="readingStore">The meter reading store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AssetService(ISession session, AssetStore assetStore, AssetTypeStore typeStore, AssetMeterReadingStore readingStore, IClock clock, IStringLocalizer<AssetService> stringLocalizer)
    {
        _session = session;
        _assetStore = assetStore;
        _typeStore = typeStore;
        _readingStore = readingStore;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Records a meter reading, refusing a meter the asset's type doesn't have and a cumulative meter going down.
    /// </summary>
    /// <param name="reading">The reading; its time defaults to now.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The reasons it was refused; empty when it was recorded.</returns>
    public async Task<IReadOnlyList<string>> RecordReadingAsync(AssetMeterReading reading, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var asset = string.IsNullOrEmpty(reading.AssetId) ? null : await _assetStore.FindByIdAsync(reading.AssetId, cancellationToken);

        if (asset is null)
        {
            return [S["The asset doesn't exist."]];
        }

        var type = await _typeStore.FindByIdAsync(asset.AssetTypeId, cancellationToken);
        var meter = type?.Meters.FirstOrDefault(item => string.Equals(item.Name, reading.Meter, StringComparison.OrdinalIgnoreCase));

        if (meter is null)
        {
            return [S["{0} has no meter called {1}.", asset.Name, reading.Meter ?? string.Empty]];
        }

        if (reading.Utc == default)
        {
            reading.Utc = _clock.UtcNow;
        }

        if (meter.Cumulative && await LatestAsync(asset.ItemId, meter.Name, reading.Utc, cancellationToken) is { } last && reading.Value < last.Value)
        {
            return [S["{0} only goes up: the last reading was {1}.", meter.Name, last.Value]];
        }

        reading.ItemId ??= CrestApps.Core.UniqueId.GenerateId();
        reading.Meter = meter.Name;

        await _readingStore.CreateAsync(reading, cancellationToken);

        return [];
    }

    /// <summary>
    /// Gives an asset's readings, newest first.
    /// </summary>
    /// <param name="assetId">The asset.</param>
    /// <param name="take">The most to give.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The readings.</returns>
    public async Task<IReadOnlyList<AssetMeterReading>> ReadingsAsync(string assetId, int take = 50, CancellationToken cancellationToken = default)
        => [.. await _session.Query<AssetMeterReading, AssetMeterReadingIndex>(index => index.AssetId == assetId, collection: AssetsConstants.CollectionName)
            .OrderByDescending(index => index.Utc)
            .Take(take)
            .ListAsync(cancellationToken)];

    /// <summary>
    /// Gives the assets directly inside an asset.
    /// </summary>
    /// <param name="assetId">The asset.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The assets, by name.</returns>
    public async Task<IReadOnlyList<Asset>> ChildrenAsync(string assetId, CancellationToken cancellationToken = default)
        => [.. (await _session.Query<Asset, AssetIndex>(index => index.ParentAssetId == assetId, collection: AssetsConstants.CollectionName).ListAsync(cancellationToken))
            .OrderBy(asset => asset.Name, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>
    /// Gives the assets an asset is inside, from the top down.
    /// </summary>
    /// <param name="asset">The asset.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The assets.</returns>
    public async Task<IReadOnlyList<Asset>> AncestorsAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var path = new List<Asset>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var parentId = asset.ParentAssetId;

        while (!string.IsNullOrEmpty(parentId) && seen.Add(parentId) && await _assetStore.FindByIdAsync(parentId, cancellationToken) is { } parent)
        {
            path.Insert(0, parent);
            parentId = parent.ParentAssetId;
        }

        return path;
    }

    private Task<AssetMeterReading> LatestAsync(string assetId, string meter, DateTime beforeUtc, CancellationToken cancellationToken)
        => _session.Query<AssetMeterReading, AssetMeterReadingIndex>(index => index.AssetId == assetId && index.Meter == meter && index.Utc <= beforeUtc, collection: AssetsConstants.CollectionName)
            .OrderByDescending(index => index.Utc)
            .FirstOrDefaultAsync(cancellationToken);
}

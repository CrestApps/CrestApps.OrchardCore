using CrestApps.OrchardCore.Assets.Models;

namespace CrestApps.OrchardCore.Assets.Services;

/// <summary>
/// Tells whether a module's records point to an asset, such as ledger postings for its purchase or trips driven in it.
/// An asset in use can't be deleted: it is retired instead, so the records keep what they point to. Register one with
/// <see cref="AssetsUsageServiceCollectionExtensions.AddAssetUsage{TUsage}"/>.
/// </summary>
public interface IAssetUsage
{
    /// <summary>
    /// Returns why the asset is in use, such as "Its purchase is posted to the ledger.", or <see langword="null"/> when
    /// no record of the module points to it.
    /// </summary>
    /// <param name="asset">The asset.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The reason, or <see langword="null"/>.</returns>
    Task<string> GetUsageAsync(Asset asset, CancellationToken cancellationToken = default);
}

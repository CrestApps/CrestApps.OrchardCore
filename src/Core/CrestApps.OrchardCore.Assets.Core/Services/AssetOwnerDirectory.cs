using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Assets.Services;

namespace CrestApps.OrchardCore.Assets.Core.Services;

/// <summary>
/// Names asset owners through the registered <see cref="IAssetOwnerSource"/>s, and says which kinds can be picked.
/// </summary>
public sealed class AssetOwnerDirectory
{
    private readonly Dictionary<AssetOwnerKind, IAssetOwnerSource> _sources;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetOwnerDirectory"/> class.
    /// </summary>
    /// <param name="sources">The owner sources.</param>
    public AssetOwnerDirectory(IEnumerable<IAssetOwnerSource> sources)
    {
        _sources = [];

        foreach (var source in sources)
        {
            _sources.TryAdd(source.Kind, source);
        }
    }

    /// <summary>
    /// Gets the kinds of owner that can be picked: Internal, then those with a source.
    /// </summary>
    public IReadOnlyList<AssetOwnerKind> Kinds
        => [AssetOwnerKind.Internal, .. Enum.GetValues<AssetOwnerKind>().Where(_sources.ContainsKey)];

    /// <summary>
    /// Gets the source of a kind of owner, or <see langword="null"/> when nothing registers one.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The source.</returns>
    public IAssetOwnerSource GetSource(AssetOwnerKind kind)
        => _sources.GetValueOrDefault(kind);

    /// <summary>
    /// Gets an owner's name, or <see langword="null"/> when it isn't found.
    /// </summary>
    /// <param name="kind">The owner's kind.</param>
    /// <param name="id">The owner's id.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The name.</returns>
    public async Task<string> GetNameAsync(AssetOwnerKind kind, string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id) || GetSource(kind) is not { } source)
        {
            return null;
        }

        return (await source.GetNamesAsync([id], cancellationToken)).GetValueOrDefault(id);
    }

    /// <summary>
    /// Gets the owners' names of several assets, keyed by kind and id.
    /// </summary>
    /// <param name="assets">The assets.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The names, keyed by <see cref="Key"/>.</returns>
    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IEnumerable<Asset> assets, CancellationToken cancellationToken = default)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var group in assets.Where(asset => asset.OwnerKind != AssetOwnerKind.Internal && !string.IsNullOrEmpty(asset.OwnerId)).GroupBy(asset => asset.OwnerKind))
        {
            if (GetSource(group.Key) is not { } source)
            {
                continue;
            }

            foreach (var (id, name) in await source.GetNamesAsync([.. group.Select(asset => asset.OwnerId).Distinct(StringComparer.Ordinal)], cancellationToken))
            {
                names[Key(group.Key, id)] = name;
            }
        }

        return names;
    }

    /// <summary>
    /// Gets the key of an owner in <see cref="GetNamesAsync(IEnumerable{Asset}, CancellationToken)"/>.
    /// </summary>
    /// <param name="kind">The owner's kind.</param>
    /// <param name="id">The owner's id.</param>
    /// <returns>The key.</returns>
    public static string Key(AssetOwnerKind kind, string id)
        => $"{kind}:{id}";
}

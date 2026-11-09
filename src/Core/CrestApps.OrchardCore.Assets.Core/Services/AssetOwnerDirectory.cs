using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;

namespace CrestApps.OrchardCore.Assets.Core.Services;

/// <summary>
/// Names asset owners through the shared parties (<see cref="IPartyDirectory"/>), and says which kinds can be picked.
/// Each <see cref="AssetOwnerKind"/> reads the party source of its party kind (<see cref="AssetOwners.ToPartyKind"/>).
/// </summary>
public sealed class AssetOwnerDirectory
{
    private readonly IPartyDirectory _parties;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetOwnerDirectory"/> class.
    /// </summary>
    /// <param name="parties">The party directory.</param>
    public AssetOwnerDirectory(IPartyDirectory parties)
    {
        _parties = parties;
    }

    /// <summary>
    /// Gets the kinds of owner that can be picked: Internal, then those with a party source.
    /// </summary>
    public IReadOnlyList<AssetOwnerKind> Kinds
        => [AssetOwnerKind.Internal, .. Enum.GetValues<AssetOwnerKind>().Where(kind => GetSource(kind) is not null)];

    /// <summary>
    /// Gets the party source of a kind of owner, or <see langword="null"/> when nothing registers one.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The source.</returns>
    public IPartySource GetSource(AssetOwnerKind kind)
        => kind.ToPartyKind() is { } partyKind ? _parties.GetSource(partyKind) : null;

    /// <summary>
    /// Gets an owner's name, or <see langword="null"/> when it isn't found.
    /// </summary>
    /// <param name="kind">The owner's kind.</param>
    /// <param name="id">The owner's id.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The name.</returns>
    public Task<string> GetNameAsync(AssetOwnerKind kind, string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id) || kind.ToPartyKind() is not { } partyKind)
        {
            return Task.FromResult<string>(null);
        }

        return _parties.GetNameAsync(new PartyRef(partyKind, id), cancellationToken);
    }

    /// <summary>
    /// Gets the owners' names of several assets, keyed by kind and id.
    /// </summary>
    /// <param name="assets">The assets.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The names, keyed by <see cref="Key"/>.</returns>
    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IEnumerable<Asset> assets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var owned = assets.Select(asset => (asset.OwnerKind, Party: asset.ToParty())).Where(owner => !owner.Party.IsEmpty).ToList();
        var found = await _parties.GetNamesAsync(owned.Select(owner => owner.Party), cancellationToken);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (kind, party) in owned)
        {
            if (found.TryGetValue(party, out var name))
            {
                names[Key(kind, party.Id)] = name;
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

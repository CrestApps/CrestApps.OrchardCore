using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Assets.Models;

/// <summary>
/// Reads an asset's owner as a party. The stored <see cref="AssetOwnerKind"/> keeps its values, so existing assets stay
/// readable; this maps each kind to the party kind its owner id belongs to.
/// </summary>
public static class AssetOwners
{
    /// <summary>
    /// Gets the party kind of an owner kind: <see cref="AssetOwnerKind.Customer"/> is a user, an account an account and a
    /// contact a contact.
    /// </summary>
    /// <param name="kind">The owner kind.</param>
    /// <returns>The party kind, or <see langword="null"/> for <see cref="AssetOwnerKind.Internal"/>.</returns>
    public static string ToPartyKind(this AssetOwnerKind kind)
        => kind switch
        {
            AssetOwnerKind.Customer => PartyKinds.User,
            AssetOwnerKind.Account => PartyKinds.Account,
            AssetOwnerKind.Contact => PartyKinds.Contact,
            _ => null,
        };

    /// <summary>
    /// Gets the party that owns an asset.
    /// </summary>
    /// <param name="asset">The asset.</param>
    /// <returns>The owner, or an empty reference when the business owns it.</returns>
    public static PartyRef ToParty(this Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        return asset.OwnerKind.ToPartyKind() is { } kind && !string.IsNullOrEmpty(asset.OwnerId)
            ? new PartyRef(kind, asset.OwnerId)
            : default;
    }
}

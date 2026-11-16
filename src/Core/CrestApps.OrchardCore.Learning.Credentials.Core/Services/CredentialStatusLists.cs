using System.Security.Cryptography;
using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// The revocation status lists. Each award holds one slot of a list for good, picked at random so the order of slots
/// says nothing about the order of awards; a list takes new awards until it is half full. The list is computed from the
/// awards, so a revocation shows in it at once.
/// </summary>
public sealed class CredentialStatusLists
{
    private const int _maximumTries = 64;

    private readonly CredentialAwardStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialStatusLists"/> class.
    /// </summary>
    /// <param name="store">The award store.</param>
    public CredentialStatusLists(CredentialAwardStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Picks a free slot. The caller holds the award lock until the award is saved.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list number and the index.</returns>
    public async Task<(int ListNumber, int ListIndex)> AllocateAsync(CancellationToken cancellationToken = default)
    {
        var listNumber = Math.Max(1, await _store.GetLastListNumberAsync(cancellationToken));

        if (await _store.CountInListAsync(listNumber, cancellationToken) >= CredentialsConstants.StatusListCapacity / 2)
        {
            listNumber++;
        }

        for (var attempt = 0; attempt < _maximumTries; attempt++)
        {
            var index = RandomNumberGenerator.GetInt32(CredentialsConstants.StatusListCapacity);

            if (!await _store.IsSlotTakenAsync(listNumber, index, cancellationToken))
            {
                return (listNumber, index);
            }
        }

        throw new InvalidOperationException($"No free slot was found in the status list {listNumber}.");
    }

    /// <summary>
    /// Encodes a status list: the bit of every revoked award is set.
    /// </summary>
    /// <param name="listNumber">The list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The encoded list.</returns>
    public async Task<string> EncodeAsync(int listNumber, CancellationToken cancellationToken = default)
        => BitstringStatusList.Encode(await _store.ListRevokedIndexesAsync(listNumber, cancellationToken), CredentialsConstants.StatusListCapacity);

    /// <summary>
    /// Reads the revocation bit of a slot from its encoded list.
    /// </summary>
    /// <param name="listNumber">The list.</param>
    /// <param name="listIndex">The index.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the slot is revoked.</returns>
    public async Task<bool> IsRevokedAsync(int listNumber, int listIndex, CancellationToken cancellationToken = default)
        => BitstringStatusList.IsSet(await EncodeAsync(listNumber, cancellationToken), listIndex);
}

using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// The revocation status lists. Each award holds one slot of a list for good, picked at random so the order of slots
/// says nothing about the order of awards; a list takes new awards until it is half full. The list is computed from the
/// awards each time it is read, so a revocation shows in it at once.
/// </summary>
public sealed class CredentialStatusLists
{
    private const int _maximumTries = 64;

    private readonly CredentialAwardStore _store;
    private readonly CredentialSigningKeys _keys;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialStatusLists"/> class.
    /// </summary>
    /// <param name="store">The award store.</param>
    /// <param name="keys">The credentials key set.</param>
    /// <param name="clock">The clock.</param>
    public CredentialStatusLists(CredentialAwardStore store, CredentialSigningKeys keys, IClock clock)
    {
        _store = store;
        _keys = keys;
        _clock = clock;
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
    /// Tells whether a status list exists: an award holds a slot in it.
    /// </summary>
    /// <param name="listNumber">The list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when it exists.</returns>
    public Task<bool> ExistsAsync(int listNumber, CancellationToken cancellationToken = default)
        => listNumber < 1 ? Task.FromResult(false) : _store.ListExistsAsync(listNumber, cancellationToken);

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

    /// <summary>
    /// Builds the status list credential, without a proof.
    /// </summary>
    /// <param name="listNumber">The list.</param>
    /// <param name="baseUrl">The site's base address, without a trailing slash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The credential.</returns>
    public async Task<JsonObject> BuildAsync(int listNumber, string baseUrl, CancellationToken cancellationToken = default)
        => StatusListCredentialBuilder.Build(baseUrl, listNumber, await EncodeAsync(listNumber, cancellationToken), _clock.UtcNow);

    /// <summary>
    /// Builds the status list credential and signs it as a JSON Web Token with the credentials key set.
    /// </summary>
    /// <param name="listNumber">The list.</param>
    /// <param name="baseUrl">The site's base address, without a trailing slash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The compact JWS.</returns>
    public async Task<string> SignAsync(int listNumber, string baseUrl, CancellationToken cancellationToken = default)
        => await _keys.SignAsync(CredentialJws.Payload(await BuildAsync(listNumber, baseUrl, cancellationToken)), baseUrl, cancellationToken);
}

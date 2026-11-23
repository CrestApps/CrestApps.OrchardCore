using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Emails;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Caps the confirmation and claim e-mails one address is sent for one list, with a counter in the distributed cache
/// (Orchard Core Rate Limits caps each visitor; this caps each mailbox, whoever asks). The key is a hash of the list
/// and the address's abuse key, so aliases share the cap and no address is kept in the cache.
/// </summary>
public sealed class MailingListConfirmationThrottle
{
    private const string KeyPrefix = "MailingLists:Confirm:";

    private readonly IDistributedCache _cache;
    private readonly MailingListsOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListConfirmationThrottle"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache.</param>
    /// <param name="options">The mailing lists options, for the cap and its window.</param>
    public MailingListConfirmationThrottle(
        IDistributedCache cache,
        IOptions<MailingListsOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    /// <summary>
    /// Returns whether another confirmation e-mail may be sent to the address for the list.
    /// </summary>
    /// <param name="listId">The list's id.</param>
    /// <param name="email">The address.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the cap is not reached.</returns>
    public async Task<bool> CanSendAsync(string listId, string email, CancellationToken cancellationToken = default)
    {
        if (_options.ConfirmationCap <= 0)
        {
            return true;
        }

        return await ReadAsync(Key(listId, email), cancellationToken) < _options.ConfirmationCap;
    }

    /// <summary>
    /// Counts one confirmation e-mail sent to the address for the list.
    /// </summary>
    /// <param name="listId">The list's id.</param>
    /// <param name="email">The address.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>A task that completes when the e-mail is counted.</returns>
    public async Task RecordAsync(string listId, string email, CancellationToken cancellationToken = default)
    {
        if (_options.ConfirmationCap <= 0)
        {
            return;
        }

        var key = Key(listId, email);
        var count = await ReadAsync(key, cancellationToken) + 1;

        await _cache.SetStringAsync(key, count.ToString(CultureInfo.InvariantCulture), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(1, _options.ConfirmationCapMinutes)),
        }, cancellationToken);
    }

    internal static string Key(string listId, string email)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{listId}\n{EmailAddresses.AbuseKey(email)}"));

        return KeyPrefix + Convert.ToHexString(bytes, 0, 16);
    }

    private async Task<int> ReadAsync(string key, CancellationToken cancellationToken)
    {
        var value = await _cache.GetStringAsync(key, cancellationToken);

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : 0;
    }
}

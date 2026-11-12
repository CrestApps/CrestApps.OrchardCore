using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// The six-digit codes emailed for signing in without a password. A code lives ten minutes and five tries; only its
/// hash is kept, in the distributed cache, per tenant. Sending is limited per email and per client address.
/// </summary>
public sealed class EmailSignInCodes
{
    public const int MaxAttempts = 5;

    private static readonly TimeSpan _lifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan _sendWindow = TimeSpan.FromMinutes(15);
    private const int MaxSendsPerEmail = 3;
    private const int MaxSendsPerAddress = 10;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly IDistributedCache _cache;
    private readonly ShellSettings _shellSettings;
    private readonly IClock _clock;
    private readonly IDistributedLock _distributedLock;

    public EmailSignInCodes(IDistributedCache cache, ShellSettings shellSettings, IClock clock, IDistributedLock distributedLock)
    {
        _cache = cache;
        _shellSettings = shellSettings;
        _clock = clock;
        _distributedLock = distributedLock;
    }

    /// <summary>
    /// Counts a send for the email and the address, and returns <see langword="false"/> when either is over its limit.
    /// </summary>
    public async Task<bool> TryCountSendAsync(string email, string address, CancellationToken cancellationToken = default)
        => await CountAsync("email:" + Normalize(email), MaxSendsPerEmail, cancellationToken) &&
           await CountAsync("address:" + (address ?? "unknown"), MaxSendsPerAddress, cancellationToken);

    /// <summary>
    /// Makes a new code for the email, replacing any earlier one, and returns it.
    /// </summary>
    public async Task<string> IssueAsync(string email, CancellationToken cancellationToken = default)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var entry = new CodeEntry(Hash(email, code), _clock.UtcNow + _lifetime, 0);

        await _cache.SetStringAsync(Key("code:" + Normalize(email)), JsonSerializer.Serialize(entry), new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = entry.ExpiresUtc,
        }, cancellationToken);

        return code;
    }

    /// <summary>
    /// Checks a code, counting the try. A right code is used up.
    /// </summary>
    public async Task<bool> VerifyAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        var key = Key("code:" + Normalize(email));

        // The read, the count and the write happen under one lock per tenant and email, and the attempt is counted before
        // the code is compared. Without both, parallel guesses all saw fewer than five attempts and a six-digit code could
        // be brute-forced. A lock that can't be taken refuses the guess rather than counting it without one.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(key + ":lock", _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return false;
        }

        await using (locker)
        {
            var json = await _cache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(json) || JsonSerializer.Deserialize<CodeEntry>(json) is not { } entry || entry.ExpiresUtc <= _clock.UtcNow || entry.Attempts >= MaxAttempts)
            {
                return false;
            }

            await _cache.SetStringAsync(key, JsonSerializer.Serialize(entry with { Attempts = entry.Attempts + 1 }), new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = entry.ExpiresUtc,
            }, cancellationToken);

            var digits = new string([.. (code ?? string.Empty).Where(char.IsAsciiDigit)]);
            var match = digits.Length == 6 && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(entry.Hash), Encoding.ASCII.GetBytes(Hash(email, digits)));

            if (match)
            {
                await _cache.RemoveAsync(key, cancellationToken);
            }

            return match;
        }
    }

    private async Task<bool> CountAsync(string name, int limit, CancellationToken cancellationToken)
    {
        var key = Key("sends:" + name);

        // Counted under a lock, like the guesses, so parallel requests can't each see room for one more send.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(key + ":lock", _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return false;
        }

        await using (locker)
        {
            var json = await _cache.GetStringAsync(key, cancellationToken);
            var now = _clock.UtcNow;
            var window = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<SendWindow>(json);

            if (window is null || window.EndsUtc <= now)
            {
                window = new SendWindow(now + _sendWindow, 0);
            }

            if (window.Count >= limit)
            {
                return false;
            }

            window = window with { Count = window.Count + 1 };

            await _cache.SetStringAsync(key, JsonSerializer.Serialize(window), new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = window.EndsUtc,
            }, cancellationToken);

            return true;
        }
    }

    private string Key(string name)
        => $"CrestApps.EmailSignIn:{_shellSettings.Name}:{name}";

    private string Hash(string email, string code)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{_shellSettings.Name}|{Normalize(email)}|{code}")));

    private static string Normalize(string email)
        => (email ?? string.Empty).Trim().ToUpperInvariant();

    private sealed record CodeEntry(string Hash, DateTime ExpiresUtc, int Attempts);

    private sealed record SendWindow(DateTime EndsUtc, int Count);
}

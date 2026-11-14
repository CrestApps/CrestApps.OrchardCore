using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Throttling;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// The six-digit codes emailed for signing in without a password. A code lives ten minutes and five tries; only its
/// hash is kept, in the distributed cache, per tenant, under a key that holds a hash of the email rather than the email.
/// Sending is limited per client network and per email, and checking per client network, by the shared attempt
/// limiter's policies, which an operator can change under <c>CrestApps_Throttling:AttemptPolicies</c>.
/// </summary>
public sealed class EmailSignInCodes
{
    public const int MaxAttempts = 5;

    /// <summary>
    /// The attempt policy that limits the codes sent to one email.
    /// </summary>
    public const string SendEmailPolicy = "email-signin-send-email";

    /// <summary>
    /// The attempt policy that limits the codes asked for from one client network.
    /// </summary>
    public const string SendNetworkPolicy = "email-signin-send-network";

    /// <summary>
    /// The attempt policy that limits the codes checked from one client network.
    /// </summary>
    public const string VerifyNetworkPolicy = "email-signin-verify-network";

    /// <summary>
    /// The rate-limit group of the sign-in code pages.
    /// </summary>
    public const string RateLimitGroup = "sign-in-code";

    private static readonly TimeSpan _lifetime = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly IDistributedCache _cache;
    private readonly IAttemptLimiter _attemptLimiter;
    private readonly ShellSettings _shellSettings;
    private readonly IClock _clock;
    private readonly IDistributedLock _distributedLock;

    public EmailSignInCodes(IDistributedCache cache, IAttemptLimiter attemptLimiter, ShellSettings shellSettings, IClock clock, IDistributedLock distributedLock)
    {
        _cache = cache;
        _attemptLimiter = attemptLimiter;
        _shellSettings = shellSettings;
        _clock = clock;
        _distributedLock = distributedLock;
    }

    /// <summary>
    /// Counts a send for the client network and then the email, and returns <see langword="false"/> when either is over
    /// its limit. The network is counted first, so a network already over its limit spends none of an email's sends.
    /// </summary>
    public async Task<bool> TryCountSendAsync(string email, string network, CancellationToken cancellationToken = default)
        => (await _attemptLimiter.AcquireAsync(SendNetworkPolicy, network ?? "unknown", cancellationToken)).Allowed &&
           (await _attemptLimiter.AcquireAsync(SendEmailPolicy, Normalize(email), cancellationToken)).Allowed;

    /// <summary>
    /// Counts a code check for the client network, and returns <see langword="false"/> when it is over its limit.
    /// </summary>
    public async Task<bool> TryCountVerifyAsync(string network, CancellationToken cancellationToken = default)
        => (await _attemptLimiter.AcquireAsync(VerifyNetworkPolicy, network ?? "unknown", cancellationToken)).Allowed;

    /// <summary>
    /// Makes a new code for the email, replacing any earlier one, and returns it.
    /// </summary>
    public async Task<string> IssueAsync(string email, CancellationToken cancellationToken = default)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var entry = new CodeEntry(Hash(email, code), _clock.UtcNow + _lifetime, 0);

        await _cache.SetStringAsync(CodeKey(email), JsonSerializer.Serialize(entry), new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = entry.ExpiresUtc,
        }, cancellationToken);

        return code;
    }

    /// <summary>
    /// Checks a code, counting the try. A right code is used up.
    /// </summary>
    public async Task<bool> VerifyAsync(string email, string code, CancellationToken cancellationToken = default)
        => await CheckAsync(email, code, cancellationToken) == EmailSignInCodeCheck.Right;

    /// <summary>
    /// Checks a code, counting the try, and tells a wrong code apart from no code to compare it with (none was sent, it
    /// expired or its tries are used up). A right code is used up.
    /// </summary>
    public async Task<EmailSignInCodeCheck> CheckAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        var key = CodeKey(email);

        // The read, the count and the write happen under one lock per tenant and email, and the attempt is counted before
        // the code is compared. Without both, parallel guesses all saw fewer than five attempts and a six-digit code could
        // be brute-forced. A lock that can't be taken refuses the guess rather than counting it without one.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(key + ":lock", _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return EmailSignInCodeCheck.NoCode;
        }

        await using (locker)
        {
            var json = await _cache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(json) || JsonSerializer.Deserialize<CodeEntry>(json) is not { } entry || entry.ExpiresUtc <= _clock.UtcNow || entry.Attempts >= MaxAttempts)
            {
                return EmailSignInCodeCheck.NoCode;
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

            return match ? EmailSignInCodeCheck.Right : EmailSignInCodeCheck.Wrong;
        }
    }

    // The email never goes into a cache key as it is: the key holds a hash of it.
    private string CodeKey(string email)
        => $"CrestApps.EmailSignIn:{_shellSettings.Name}:code:{Sha256Hex($"{_shellSettings.Name}|{Normalize(email)}")}";

    private string Hash(string email, string code)
        => Sha256Hex($"{_shellSettings.Name}|{Normalize(email)}|{code}");

    private static string Sha256Hex(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Normalize(string email)
        => (email ?? string.Empty).Trim().ToUpperInvariant();

    private sealed record CodeEntry(string Hash, DateTime ExpiresUtc, int Attempts);
}

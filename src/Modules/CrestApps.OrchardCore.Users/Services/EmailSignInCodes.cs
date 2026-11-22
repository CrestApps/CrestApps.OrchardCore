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
/// The eight-digit codes emailed for signing in without a password. A code lives ten minutes and five tries; only its
/// hash is kept, in the distributed cache, per tenant, under a key that holds a hash of the account's id. A code is for
/// one account and the address it was mailed to: it is keyed on the account, not on what was typed, because a database
/// that compares text loosely (SQL Server's default collation treats <c>AE</c> as <c>Æ</c>) finds an account for a
/// look-alike address; and its hash holds the address, so it stops working if the account's address changes.
/// Sending is limited per client network, per email (normalized as the account lookup normalizes it) and per account
/// each day; checking per client network and per account, whose wrong codes block its code checks for a day once they
/// reach the limit. The shared attempt limiter's policies count them, which an operator can change under
/// <c>CrestApps_Throttling:AttemptPolicies</c>. With eight digits, five tries a code and ten wrong codes an account a
/// day, a guesser has about one chance in ten million a day.
/// </summary>
public sealed class EmailSignInCodes
{
    public const int MaxAttempts = 5;

    /// <summary>
    /// How many digits a code has.
    /// </summary>
    public const int CodeLength = 8;

    /// <summary>
    /// The attempt policy that limits the codes made for one account, whatever address or network asked: a daily cap, so
    /// neither guessing nor mail-bombing gets a fresh supply of codes.
    /// </summary>
    public const string SendAccountPolicy = "email-signin-send-account";

    /// <summary>
    /// The attempt policy that counts the wrong codes posted for one account, from any network: once it reaches its limit
    /// the account's codes are not checked until the block ends.
    /// </summary>
    public const string VerifyAccountPolicy = "email-signin-verify-account";

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
    /// its limit. The network is counted first, so a network already over its limit spends none of an email's sends. The
    /// email is counted as the account lookup normalizes it, so every form of it that finds the same account (the
    /// Kelvin sign for a K) shares one count.
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
    /// Makes a new code for the account, replacing any earlier one, and returns it; or <see langword="null"/> when the
    /// account has had all the codes it may have today.
    /// </summary>
    /// <param name="userId">The account's id.</param>
    /// <param name="email">The account's own address, which the code is mailed to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<string> IssueAsync(string userId, string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        if (!(await _attemptLimiter.AcquireAsync(SendAccountPolicy, userId, cancellationToken)).Allowed)
        {
            return null;
        }

        var code = RandomNumberGenerator.GetInt32(0, 100_000_000).ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
        var entry = new CodeEntry(Hash(userId, email, code), _clock.UtcNow + _lifetime, 0);

        await _cache.SetStringAsync(CodeKey(userId), JsonSerializer.Serialize(entry), new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = entry.ExpiresUtc,
        }, cancellationToken);

        return code;
    }

    /// <summary>
    /// Checks a code, counting the try. A right code is used up.
    /// </summary>
    /// <param name="userId">The account's id.</param>
    /// <param name="email">The account's own address now.</param>
    /// <param name="code">The code that was typed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<bool> VerifyAsync(string userId, string email, string code, CancellationToken cancellationToken = default)
        => await CheckAsync(userId, email, code, cancellationToken) == EmailSignInCodeCheck.Right;

    /// <summary>
    /// Checks a code, counting the try, and tells a wrong code apart from no code to compare it with (none was sent, it
    /// expired or its tries are used up). A right code is used up. A code mailed to an address the account no longer
    /// has is wrong. A wrong code counts toward the account's limit; once that is reached, no code of the account is
    /// compared, the right one included, until the block ends (<see cref="EmailSignInCodeCheck.Blocked"/>).
    /// </summary>
    /// <param name="userId">The account's id.</param>
    /// <param name="email">The account's own address now.</param>
    /// <param name="code">The code that was typed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<EmailSignInCodeCheck> CheckAsync(string userId, string email, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return EmailSignInCodeCheck.NoCode;
        }

        var key = CodeKey(userId);

        // The read, the count and the write happen under one lock per tenant and email, and the attempt is counted before
        // the code is compared. Without both, parallel guesses all saw fewer than five attempts and a code could be
        // brute-forced. A lock that can't be taken refuses the guess rather than counting it without one.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(key + ":lock", _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return EmailSignInCodeCheck.NoCode;
        }

        await using (locker)
        {
            if (!(await _attemptLimiter.PeekAsync(VerifyAccountPolicy, userId, cancellationToken)).Allowed)
            {
                return EmailSignInCodeCheck.Blocked;
            }

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
            var match = digits.Length == CodeLength && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(entry.Hash), Encoding.ASCII.GetBytes(Hash(userId, email, digits)));

            if (!match)
            {
                await _attemptLimiter.RecordFailureAsync(VerifyAccountPolicy, userId, cancellationToken);

                return EmailSignInCodeCheck.Wrong;
            }

            await _cache.RemoveAsync(key, cancellationToken);

            return EmailSignInCodeCheck.Right;
        }
    }

    /// <summary>
    /// The id the code pages count and check an address under when no account a code can reach has it, so that path
    /// does the same work as one for an account (the limits, the code's cache entry, its check) and takes the same time.
    /// It can never be an account's id, and a code made for it is never mailed.
    /// </summary>
    /// <param name="email">The typed address.</param>
    public string GetStandInId(string email)
        => "no-account:" + Sha256Hex($"{_shellSettings.Name}|no-account|{Normalize(email)}");

    // The key holds a hash of the account's id, compared exactly; nothing typed goes into it.
    private string CodeKey(string userId)
        => $"CrestApps.EmailSignIn:{_shellSettings.Name}:code:{Sha256Hex($"{_shellSettings.Name}|user|{userId}")}";

    private string Hash(string userId, string email, string code)
        => Sha256Hex($"{_shellSettings.Name}|{userId}|{Normalize(email)}|{code}");

    private static string Sha256Hex(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    // As Orchard Core's lookup normalizer does, so every form of an address that finds an account counts as one.
    private static string Normalize(string email)
        => (email ?? string.Empty).Trim().Normalize().ToUpperInvariant();

    private sealed record CodeEntry(string Hash, DateTime ExpiresUtc, int Attempts);
}

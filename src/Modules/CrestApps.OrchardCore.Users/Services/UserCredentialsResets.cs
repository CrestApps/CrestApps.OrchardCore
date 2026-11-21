using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// When an account's earlier credentials were reset, so that a session started before then is refused at once. ASP.NET
/// Core Identity compares a session's security stamp with the account's only every
/// <see cref="SecurityStampValidatorOptions.ValidationInterval"/> (30 minutes by default), so rotating the stamp alone
/// leaves an earlier session working until then. The time is kept on the user, and, for that interval, in the
/// distributed cache with the new security stamp, which the sign-in cookie's validation reads on every request; after
/// the interval the stamp check refuses those sessions itself. The same entry also asks for earlier sessions' claims to
/// be refreshed (see <see cref="PublishClaimsRefreshAsync"/>), so the validation reads one entry per request either way.
/// </summary>
public sealed class UserCredentialsResets
{
    /// <summary>
    /// The key of the time, in the user's properties, when the account's earlier credentials were reset.
    /// </summary>
    public const string PropertyName = "CredentialsResetUtc";

    // The reset time and the new security stamp are kept as one value, split by this.
    private const char Separator = '|';

    // A little longer than the interval, for the clocks of several servers.
    private static readonly TimeSpan _margin = TimeSpan.FromMinutes(1);

    private readonly IDistributedCache _cache;
    private readonly ShellSettings _shellSettings;
    private readonly IOptions<SecurityStampValidatorOptions> _stampOptions;
    private readonly ILogger _logger;

    public UserCredentialsResets(
        IDistributedCache cache,
        ShellSettings shellSettings,
        IOptions<SecurityStampValidatorOptions> stampOptions,
        ILogger<UserCredentialsResets> logger)
    {
        _cache = cache;
        _shellSettings = shellSettings;
        _stampOptions = stampOptions;
        _logger = logger;
    }

    /// <summary>
    /// Writes the time of the reset on the user, to be saved with it.
    /// </summary>
    /// <param name="properties">The user's properties.</param>
    /// <param name="resetUtc">When the credentials were reset.</param>
    public static void Record(JsonObject properties, DateTime resetUtc)
    {
        ArgumentNullException.ThrowIfNull(properties);

        properties[PropertyName] = resetUtc.ToString("O", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Makes the reset refuse earlier sessions, once it is saved.
    /// </summary>
    /// <param name="userId">The account's id.</param>
    /// <param name="resetUtc">When the credentials were reset.</param>
    /// <param name="securityStamp">The security stamp the reset gave the account, which the sessions it signs in carry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task PublishAsync(string userId, DateTime resetUtc, string securityStamp, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var value = resetUtc.ToString("O", CultureInfo.InvariantCulture) + Separator + securityStamp;

        return _cache.SetStringAsync(Key(userId), value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _stampOptions.Value.ValidationInterval + _margin,
        }, cancellationToken);
    }

    /// <summary>
    /// Makes every session of the account issued before <paramref name="refreshUtc"/> have its claims built again from
    /// the account at its next request, as Identity's security stamp check does at its next run, without signing it out:
    /// the account was marked in a way its sessions' claims carry (see
    /// <see cref="EmailOwnershipUserEventHandler"/>). A reset already published for the account is kept.
    /// </summary>
    /// <param name="userId">The account's id.</param>
    /// <param name="refreshUtc">When the account was marked.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task PublishClaimsRefreshAsync(string userId, DateTime refreshUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var (resetText, stamp, _) = Split(await ReadAsync(userId, cancellationToken));
        var value = resetText + Separator + stamp + Separator + refreshUtc.ToString("O", CultureInfo.InvariantCulture);

        await _cache.SetStringAsync(Key(userId), value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _stampOptions.Value.ValidationInterval + _margin,
        }, cancellationToken);
    }

    /// <summary>
    /// Whether a session of the account, issued at <paramref name="issuedUtc"/> with <paramref name="securityStamp"/>,
    /// started before its credentials were reset. When the cache can't be read the session is let through to Identity's
    /// own stamp check, which refuses it at its next run, rather than failing every signed-in request.
    /// </summary>
    /// <param name="userId">The account's id.</param>
    /// <param name="issuedUtc">When the session's cookie was issued.</param>
    /// <param name="securityStamp">The security stamp the session's cookie carries.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<bool> IsBeforeResetAsync(string userId, DateTimeOffset? issuedUtc, string securityStamp, CancellationToken cancellationToken = default)
        => await CheckAsync(userId, issuedUtc, securityStamp, cancellationToken) == CredentialsResetCheck.Refuse;

    /// <summary>
    /// What the sign-in cookie's validation does with a session of the account, issued at <paramref name="issuedUtc"/>
    /// with <paramref name="securityStamp"/>: refuse it when it started before the account's credentials were reset,
    /// refresh it when it started before a claims refresh (see <see cref="PublishClaimsRefreshAsync"/>), and otherwise
    /// nothing. One cache read, whatever the answer. When the cache can't be read, nothing.
    /// </summary>
    /// <param name="userId">The account's id.</param>
    /// <param name="issuedUtc">When the session's cookie was issued.</param>
    /// <param name="securityStamp">The security stamp the session's cookie carries.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<CredentialsResetCheck> CheckAsync(string userId, DateTimeOffset? issuedUtc, string securityStamp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || await ReadAsync(userId, cancellationToken) is not { Length: > 0 } text)
        {
            return CredentialsResetCheck.None;
        }

        var (resetText, stamp, refreshText) = Split(text);

        if (TryParse(resetText, out var resetUtc) && IsBeforeReset(resetUtc, stamp, issuedUtc, securityStamp))
        {
            return CredentialsResetCheck.Refuse;
        }

        // Within the refresh's own second a cookie's issue time can't tell a session issued just before it from one just
        // after, so both are refreshed; refreshing a session whose claims are already current changes nothing.
        return TryParse(refreshText, out var refreshUtc) && (issuedUtc is not { } issued || issued < Second(refreshUtc).AddSeconds(1))
            ? CredentialsResetCheck.Refresh
            : CredentialsResetCheck.None;
    }

    private static bool IsBeforeReset(DateTime resetUtc, string stamp, DateTimeOffset? issuedUtc, string securityStamp)
    {
        // A cookie keeps its issue time to the second, so within the reset's own second the time can't tell the session
        // the reset signs in from one issued just before it; the security stamp the reset gave the account can.
        var resetSecond = Second(resetUtc);

        if (issuedUtc is not { } issued || issued < resetSecond)
        {
            return true;
        }

        return issued < resetSecond.AddSeconds(1) &&
            !string.IsNullOrEmpty(stamp) &&
            !string.Equals(stamp, securityStamp, StringComparison.Ordinal);
    }

    // The value is "reset|stamp" or "reset|stamp|refresh", where the reset and its stamp are empty for a refresh alone.
    private static (string Reset, string Stamp, string Refresh) Split(string text)
    {
        var parts = (text ?? string.Empty).Split(Separator);

        return (parts[0], parts.Length > 1 ? parts[1] : string.Empty, parts.Length > 2 ? parts[2] : string.Empty);
    }

    private static bool TryParse(string text, out DateTime utc)
        => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out utc);

    private static DateTimeOffset Second(DateTime utc)
        => new(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);

    private async Task<string> ReadAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            return await _cache.GetStringAsync(Key(userId), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The credentials reset of a user could not be read; their session is left to the security stamp check.");

            return null;
        }
    }

    private string Key(string userId)
        => $"CrestApps.Users:{_shellSettings.Name}:credentials-reset:{userId}";
}

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
/// the interval the stamp check refuses those sessions itself.
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
    /// Whether a session of the account, issued at <paramref name="issuedUtc"/> with <paramref name="securityStamp"/>,
    /// started before its credentials were reset. When the cache can't be read the session is let through to Identity's
    /// own stamp check, which refuses it at its next run, rather than failing every signed-in request.
    /// </summary>
    /// <param name="userId">The account's id.</param>
    /// <param name="issuedUtc">When the session's cookie was issued.</param>
    /// <param name="securityStamp">The security stamp the session's cookie carries.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<bool> IsBeforeResetAsync(string userId, DateTimeOffset? issuedUtc, string securityStamp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || await ReadAsync(userId, cancellationToken) is not { Length: > 0 } text)
        {
            return false;
        }

        var separator = text.IndexOf(Separator, StringComparison.Ordinal);
        var stamp = separator < 0 ? null : text[(separator + 1)..];

        if (!DateTime.TryParse(separator < 0 ? text : text[..separator], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var resetUtc))
        {
            return false;
        }

        // A cookie keeps its issue time to the second, so within the reset's own second the time can't tell the session
        // the reset signs in from one issued just before it; the security stamp the reset gave the account can.
        var resetSecond = new DateTimeOffset(resetUtc.Ticks - (resetUtc.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);

        if (issuedUtc is not { } issued || issued < resetSecond)
        {
            return true;
        }

        return issued < resetSecond.AddSeconds(1) &&
            !string.IsNullOrEmpty(stamp) &&
            !string.Equals(stamp, securityStamp, StringComparison.Ordinal);
    }

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

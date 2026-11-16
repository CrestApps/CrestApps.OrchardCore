using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// When an account's earlier credentials were reset, so that a session started before then is refused at once. ASP.NET
/// Core Identity compares a session's security stamp with the account's only every
/// <see cref="SecurityStampValidatorOptions.ValidationInterval"/> (30 minutes by default), so rotating the stamp alone
/// leaves an earlier session working until then. The time is kept on the user, and, for that interval, in the
/// distributed cache, which the sign-in cookie's validation reads on every request; after the interval the stamp check
/// refuses those sessions itself.
/// </summary>
public sealed class UserCredentialsResets
{
    /// <summary>
    /// The key of the time, in the user's properties, when the account's earlier credentials were reset.
    /// </summary>
    public const string PropertyName = "CredentialsResetUtc";

    // A little longer than the interval, for the clocks of several servers.
    private static readonly TimeSpan _margin = TimeSpan.FromMinutes(1);

    private readonly IDistributedCache _cache;
    private readonly ShellSettings _shellSettings;
    private readonly IOptions<SecurityStampValidatorOptions> _stampOptions;

    public UserCredentialsResets(IDistributedCache cache, ShellSettings shellSettings, IOptions<SecurityStampValidatorOptions> stampOptions)
    {
        _cache = cache;
        _shellSettings = shellSettings;
        _stampOptions = stampOptions;
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
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task PublishAsync(string userId, DateTime resetUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return _cache.SetStringAsync(Key(userId), resetUtc.ToString("O", CultureInfo.InvariantCulture), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _stampOptions.Value.ValidationInterval + _margin,
        }, cancellationToken);
    }

    /// <summary>
    /// Whether a session of the account issued at <paramref name="issuedUtc"/> started before its credentials were reset.
    /// </summary>
    /// <param name="userId">The account's id.</param>
    /// <param name="issuedUtc">When the session's cookie was issued.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<bool> IsBeforeResetAsync(string userId, DateTimeOffset? issuedUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) ||
            await _cache.GetStringAsync(Key(userId), cancellationToken) is not { Length: > 0 } text ||
            !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var resetUtc))
        {
            return false;
        }

        // A cookie keeps its issue time to the second, so the reset is compared to the second too: the session the reset
        // itself signs in, in the same second, is kept.
        var resetSecond = new DateTimeOffset(resetUtc.Ticks - (resetUtc.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);

        return issuedUtc is not { } issued || issued < resetSecond;
    }

    private string Key(string userId)
        => $"CrestApps.Users:{_shellSettings.Name}:credentials-reset:{userId}";
}

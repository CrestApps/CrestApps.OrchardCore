using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Makes and reads signed links to a recording's audio that work without signing in until they expire, for sharing
/// a recording with someone outside the admin, or handing it to another system.
/// </summary>
public sealed class CallRecordingLinks
{
    /// <summary>
    /// The path the links are served under.
    /// </summary>
    public const string RoutePrefix = "call-recordings/shared";

    /// <summary>
    /// The longest a link may last.
    /// </summary>
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// How long a link lasts when no time is given.
    /// </summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(24);

    private const string Purpose = "CrestApps.ContactCenter.CallRecordingLinks.v1";

    private readonly IDataProtector _protector;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallRecordingLinks"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">Signs and encrypts the links with the tenant's keys.</param>
    /// <param name="clock">The clock.</param>
    public CallRecordingLinks(IDataProtectionProvider dataProtectionProvider, IClock clock)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _clock = clock;
    }

    /// <summary>
    /// Makes the token of a link to a recording.
    /// </summary>
    /// <param name="recordingId">The recording.</param>
    /// <param name="lifetime">How long the link lasts, at most <see cref="MaxLifetime"/>.</param>
    /// <returns>The token and when it expires.</returns>
    public (string Token, DateTime ExpiresUtc) Create(string recordingId, TimeSpan? lifetime = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(recordingId);

        var span = lifetime is { } requested && requested > TimeSpan.Zero
            ? requested > MaxLifetime ? MaxLifetime : requested
            : DefaultLifetime;
        var expires = _clock.UtcNow.Add(span);

        // The expiry is inside the signed payload and checked against the tenant's clock.
        var payload = $"{expires.Ticks.ToString(CultureInfo.InvariantCulture)}|{recordingId}";

        return (_protector.Protect(payload), expires);
    }

    /// <summary>
    /// Reads the recording a token links to.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The recording, or <see langword="null"/> when the token is not valid or has expired.</returns>
    public string Read(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            var payload = _protector.Unprotect(token);
            var separator = payload.IndexOf('|');

            if (separator <= 0 || !long.TryParse(payload.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var ticks))
            {
                return null;
            }

            return new DateTime(ticks, DateTimeKind.Utc) > _clock.UtcNow ? payload[(separator + 1)..] : null;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }
}

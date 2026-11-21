using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// Signed, short-lived message hints: the tenant's ASP.NET Core Data Protection with a purpose of its own, the expiry
/// inside the protected payload, checked against the site clock. A hint that was altered, made for another purpose or
/// tenant, or has expired reads as <see langword="null"/>.
/// </summary>
public sealed class LtiMessageHints
{
    /// <summary>
    /// How long a hint is valid: long enough for the tool's login redirect, short enough that a copied URL is useless.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private const string Purpose = "CrestApps.OrchardCore.Learning.Lti.MessageHint";

    private readonly IDataProtector _protector;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="LtiMessageHints"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The tenant's data protection.</param>
    /// <param name="clock">The clock.</param>
    public LtiMessageHints(IDataProtectionProvider dataProtectionProvider, IClock clock)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _clock = clock;
    }

    /// <summary>
    /// Makes a hint, valid for <see cref="Lifetime"/> from now.
    /// </summary>
    /// <param name="hint">What the launch is for.</param>
    /// <returns>The protected hint.</returns>
    public string Create(LtiMessageHint hint)
    {
        ArgumentNullException.ThrowIfNull(hint);

        hint.ExpiresUtc = _clock.UtcNow + Lifetime;

        return _protector.Protect(JsonSerializer.Serialize(hint));
    }

    /// <summary>
    /// Reads a hint.
    /// </summary>
    /// <param name="value">The protected hint.</param>
    /// <returns>The hint, or <see langword="null"/> when it is not valid.</returns>
    public LtiMessageHint Read(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 8192)
        {
            return null;
        }

        try
        {
            var hint = JsonSerializer.Deserialize<LtiMessageHint>(_protector.Unprotect(value));

            return hint is null || hint.ExpiresUtc <= _clock.UtcNow ? null : hint;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }
}

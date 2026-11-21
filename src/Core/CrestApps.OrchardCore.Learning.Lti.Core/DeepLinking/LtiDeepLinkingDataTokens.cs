using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Lti.Core.DeepLinking;

/// <summary>
/// Signs the <c>data</c> of a Deep Linking request with the tenant's Data Protection (a purpose of its own, the expiry
/// inside), so a response is accepted only for the request the platform made.
/// </summary>
public sealed class LtiDeepLinkingDataTokens
{
    /// <summary>
    /// How long the person has to pick content in the tool.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private const string Purpose = "CrestApps.OrchardCore.Learning.Lti.DeepLinkingData";

    private readonly IDataProtector _protector;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="LtiDeepLinkingDataTokens"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The tenant's data protection.</param>
    /// <param name="clock">The clock.</param>
    public LtiDeepLinkingDataTokens(IDataProtectionProvider dataProtectionProvider, IClock clock)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _clock = clock;
    }

    /// <summary>
    /// Signs data, with a new id and valid for <see cref="Lifetime"/> from now.
    /// </summary>
    /// <param name="data">The request.</param>
    /// <returns>The signed data.</returns>
    public string Create(LtiDeepLinkingData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        data.Id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        data.ExpiresUtc = _clock.UtcNow + Lifetime;

        return _protector.Protect(JsonSerializer.Serialize(data));
    }

    /// <summary>
    /// Reads signed data.
    /// </summary>
    /// <param name="value">The signed data.</param>
    /// <returns>The data, or <see langword="null"/> when it was altered, made elsewhere or expired.</returns>
    public LtiDeepLinkingData Read(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 8192)
        {
            return null;
        }

        try
        {
            var data = JsonSerializer.Deserialize<LtiDeepLinkingData>(_protector.Unprotect(value));

            return data is null || data.ExpiresUtc <= _clock.UtcNow ? null : data;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }
}

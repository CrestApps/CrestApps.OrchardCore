using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Core.Security;

/// <summary>
/// Protects and reads the secrets a settings object stores, such as an API key or a connection string, with the
/// tenant's data protection keys.
/// </summary>
/// <remarks>
/// A secret is stored protected for one purpose and read back with the same purpose. An empty value stays empty, so a
/// setting that holds no secret needs no special case. A protected value written with keys the tenant no longer has
/// (after a key ring was lost or the settings were copied from another environment) can't be read; read it with
/// <see cref="UnprotectSettingOrDefault"/> where the feature should treat it as not entered, and with
/// <see cref="UnprotectSetting"/> where the caller must stop.
/// </remarks>
public static class ProtectedSettingExtensions
{
    /// <summary>
    /// Protects a secret for storage in settings.
    /// </summary>
    /// <param name="dataProtectionProvider">The tenant's data protection provider.</param>
    /// <param name="purpose">The purpose the secret is protected for.</param>
    /// <param name="value">The secret in clear text.</param>
    /// <returns>The protected secret, or <paramref name="value"/> itself when it is empty.</returns>
    public static string ProtectSetting(
        this IDataProtectionProvider dataProtectionProvider,
        string purpose,
        string value)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return dataProtectionProvider.CreateProtector(purpose).Protect(value);
    }

    /// <summary>
    /// Reads a protected secret from settings.
    /// </summary>
    /// <param name="dataProtectionProvider">The tenant's data protection provider.</param>
    /// <param name="purpose">The purpose the secret was protected for.</param>
    /// <param name="protectedValue">The stored, protected secret.</param>
    /// <returns>The secret in clear text, or <see langword="null"/> when none is stored.</returns>
    /// <exception cref="CryptographicException">The stored value can't be read with the tenant's keys.</exception>
    public static string UnprotectSetting(
        this IDataProtectionProvider dataProtectionProvider,
        string purpose,
        string protectedValue)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        if (string.IsNullOrEmpty(protectedValue))
        {
            return null;
        }

        return dataProtectionProvider.CreateProtector(purpose).Unprotect(protectedValue);
    }

    /// <summary>
    /// Reads a protected secret from settings, and logs a warning and treats the secret as not entered when it can't
    /// be read.
    /// </summary>
    /// <param name="dataProtectionProvider">The tenant's data protection provider.</param>
    /// <param name="purpose">The purpose the secret was protected for.</param>
    /// <param name="protectedValue">The stored, protected secret.</param>
    /// <param name="logger">The logger the warning is written to.</param>
    /// <param name="settingName">The name of the secret in the warning, such as <c>EasyPost API key</c>.</param>
    /// <returns>The secret in clear text, or <see langword="null"/> when none is stored or it can't be read.</returns>
    public static string UnprotectSettingOrDefault(
        this IDataProtectionProvider dataProtectionProvider,
        string purpose,
        string protectedValue,
        ILogger logger,
        string settingName)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            return dataProtectionProvider.UnprotectSetting(purpose, protectedValue);
        }
        catch (CryptographicException exception)
        {
            logger.LogWarning(exception, "The stored {SettingName} couldn't be read with this site's data protection keys; enter it again in its settings.", settingName);

            return null;
        }
    }
}

using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.Payroll.DataSources;

/// <summary>
/// Carries a person's full tax id from a filing data set (the W-2 wage records, the T4 slips) to the file format that
/// writes the agency's file, encrypted: a preview, a CSV or any other file shows ciphertext, and only the agency file
/// formats read it back, within a day. A value a pipeline step made up can't be read.
/// </summary>
public static class FileTaxIds
{
    private const string Purpose = "CrestApps.OrchardCore.Payroll.FileTaxIds";

    private static readonly TimeSpan _lifetime = TimeSpan.FromDays(1);

    /// <summary>
    /// Encrypts a tax id for a file format.
    /// </summary>
    /// <param name="provider">The data protection provider.</param>
    /// <param name="taxId">The full tax id.</param>
    /// <returns>The encrypted value, or <see langword="null"/>.</returns>
    public static string Protect(IDataProtectionProvider provider, string taxId)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return string.IsNullOrEmpty(taxId) ? null : provider.CreateProtector(Purpose).ToTimeLimitedDataProtector().Protect(taxId, _lifetime);
    }

    /// <summary>
    /// Reads back a tax id a data set encrypted.
    /// </summary>
    /// <param name="provider">The data protection provider.</param>
    /// <param name="value">The encrypted value.</param>
    /// <returns>The tax id, or <see langword="null"/> when the value is empty, expired or not one this site encrypted.</returns>
    public static string Unprotect(IDataProtectionProvider provider, string value)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            return provider.CreateProtector(Purpose).ToTimeLimitedDataProtector().Unprotect(value);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}

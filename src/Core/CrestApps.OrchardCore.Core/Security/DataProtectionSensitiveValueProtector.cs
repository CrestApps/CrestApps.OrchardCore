using System.Security.Cryptography;
using CrestApps.OrchardCore.SensitiveData;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.Core.Security;

/// <summary>
/// The <see cref="ISensitiveValueProtector"/> on ASP.NET Core Data Protection: each value is protected with the
/// tenant's key ring for its purpose.
/// </summary>
public sealed class DataProtectionSensitiveValueProtector : ISensitiveValueProtector
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataProtectionSensitiveValueProtector"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The tenant's data protection provider.</param>
    public DataProtectionSensitiveValueProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _dataProtectionProvider = dataProtectionProvider;
    }

    /// <inheritdoc />
    public ProtectedValue Protect(string purpose, string value, int visibleCharacters = 4)
    {
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return new ProtectedValue
        {
            Cipher = _dataProtectionProvider.CreateProtector(purpose).Protect(value),
            Hint = SensitiveMask.LastCharacters(value, visibleCharacters),
        };
    }

    /// <inheritdoc />
    public string Unprotect(string purpose, ProtectedValue value)
    {
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        if (value is null || !value.HasValue)
        {
            return null;
        }

        return _dataProtectionProvider.CreateProtector(purpose).Unprotect(value.Cipher);
    }

    /// <inheritdoc />
    public bool TryUnprotect(string purpose, ProtectedValue value, out string clearText)
    {
        try
        {
            clearText = Unprotect(purpose, value);

            return true;
        }
        catch (CryptographicException)
        {
            clearText = null;

            return false;
        }
    }
}

namespace CrestApps.OrchardCore.SensitiveData;

/// <summary>
/// Protects the sensitive values records store, such as bank account numbers and tax numbers, with the tenant's data
/// protection keys, and reads them back.
/// </summary>
/// <remarks>
/// A value is protected for one purpose and read back with the same purpose; use one purpose per kind of value and
/// never change it. A value protected with keys the tenant no longer has (after a key ring was lost, or the database
/// was copied to another environment) can't be read and must be entered again.
/// </remarks>
public interface ISensitiveValueProtector
{
    /// <summary>
    /// Protects a value for storage.
    /// </summary>
    /// <param name="purpose">The purpose the value is protected for, such as <c>Banking.AccountNumber</c>.</param>
    /// <param name="value">The value in clear text.</param>
    /// <param name="visibleCharacters">How many of the last characters to keep in clear text as the hint. Never more than half of the value is kept.</param>
    /// <returns>The protected value, or <see langword="null"/> when <paramref name="value"/> is empty.</returns>
    ProtectedValue Protect(string purpose, string value, int visibleCharacters = 4);

    /// <summary>
    /// Reads a protected value.
    /// </summary>
    /// <param name="purpose">The purpose the value was protected for.</param>
    /// <param name="value">The stored value.</param>
    /// <returns>The value in clear text, or <see langword="null"/> when none is stored.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">The stored value can't be read with the tenant's keys.</exception>
    string Unprotect(string purpose, ProtectedValue value);

    /// <summary>
    /// Reads a protected value, and reports instead of throwing when it can't be read with the tenant's keys.
    /// </summary>
    /// <param name="purpose">The purpose the value was protected for.</param>
    /// <param name="value">The stored value.</param>
    /// <param name="clearText">The value in clear text, or <see langword="null"/> when none is stored or it can't be read.</param>
    /// <returns><see langword="false"/> when a value is stored and can't be read; otherwise <see langword="true"/>.</returns>
    bool TryUnprotect(string purpose, ProtectedValue value, out string clearText);
}

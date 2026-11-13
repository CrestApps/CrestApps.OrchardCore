using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.SensitiveData;

/// <summary>
/// A sensitive value a record stores protected, such as a bank account number or a person's tax number, with the last
/// characters kept in clear text so a screen can show which value it is without reading it.
/// </summary>
/// <remarks>
/// Create one with <see cref="ISensitiveValueProtector.Protect"/> and read it with
/// <see cref="ISensitiveValueProtector.Unprotect"/>. This is for values that belong to a record, one per person or per
/// account. A credential of an outside service (an API key, a password) belongs in Orchard Core Secrets instead.
/// </remarks>
public sealed class ProtectedValue
{
    /// <summary>
    /// Gets or sets the value, protected with the tenant's data protection keys.
    /// </summary>
    public string Cipher { get; set; }

    /// <summary>
    /// Gets or sets the last characters of the value, in clear text, for display.
    /// </summary>
    public string Hint { get; set; }

    /// <summary>
    /// Gets whether a value is stored.
    /// </summary>
    [JsonIgnore]
    public bool HasValue => !string.IsNullOrEmpty(Cipher);

    /// <summary>
    /// Returns the masked value, so a protected value written to a log or a page by mistake shows no more than its hint.
    /// </summary>
    public override string ToString()
        => SensitiveMask.Mask(this);
}

namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents the result of querying telephony providers for a user's active calls.
/// </summary>
public sealed class TelephonyCallListLookupResult
{
    /// <summary>
    /// Gets or sets a value indicating whether every provider lookup completed successfully.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the provider-authoritative active calls.
    /// </summary>
    public IReadOnlyList<TelephonyCall> Calls { get; set; } = [];

    /// <summary>
    /// Gets or sets the error message when a provider lookup failed.
    /// </summary>
    public string Error { get; set; }
}

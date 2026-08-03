namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents the result of querying a telephony provider for the current state of a call.
/// </summary>
public sealed class TelephonyCallLookupResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the lookup completed successfully.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider still reports the call.
    /// </summary>
    public bool Found { get; set; }

    /// <summary>
    /// Gets or sets the current provider call state when the lookup succeeded and found the call.
    /// </summary>
    public TelephonyCall Call { get; set; }

    /// <summary>
    /// Gets or sets the error message when the lookup failed.
    /// </summary>
    public string Error { get; set; }
}

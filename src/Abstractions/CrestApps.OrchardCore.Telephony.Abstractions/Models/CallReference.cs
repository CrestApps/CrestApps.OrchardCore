namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Identifies an existing call for operations that act on a single call.
/// </summary>
public sealed class CallReference
{
    /// <summary>
    /// Gets or sets the provider-specific identifier of the call.
    /// </summary>
    public string CallId { get; set; }
}

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

    /// <summary>
    /// Gets or sets optional provider-neutral metadata associated with the call action.
    /// Providers can inspect this bag for routing or policy hints without requiring new shared
    /// interface properties for each integration-specific scenario.
    /// </summary>
    public IDictionary<string, object> Metadata { get; set; }
}

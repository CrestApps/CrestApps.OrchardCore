namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// Represents a request to complete a subscription session with the Pay Later processor.
/// </summary>
public sealed class PayLaterRequest
{
    /// <summary>
    /// Gets or sets the subscription session identifier.
    /// </summary>
    public string SessionId { get; set; }
}

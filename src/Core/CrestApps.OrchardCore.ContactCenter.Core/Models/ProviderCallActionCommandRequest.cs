namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Describes a durable provider action for an existing call.
/// </summary>
public sealed class ProviderCallActionCommandRequest
{
    /// <summary>
    /// Gets or sets the CRM activity identifier.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the queue identifier that owned the unanswered offer.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the provider call identifier.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets whether a definitive provider failure should return the live call to routing.
    /// </summary>
    public bool ReofferOnFailure { get; set; }

    /// <summary>
    /// Gets or sets provider request metadata.
    /// </summary>
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}

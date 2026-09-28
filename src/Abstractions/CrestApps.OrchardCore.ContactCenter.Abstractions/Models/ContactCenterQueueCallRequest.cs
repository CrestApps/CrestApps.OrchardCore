namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents a request to place or move a provider call into a provider-side queue.
/// </summary>
public sealed class ContactCenterQueueCallRequest
{
    /// <summary>
    /// Gets or sets the CRM activity identifier associated with the queued call.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the Contact Center interaction identifier.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the provider call identifier.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the Contact Center queue identifier.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the priority to apply when the provider supports provider-side queue priority.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets provider-specific queue metadata.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}

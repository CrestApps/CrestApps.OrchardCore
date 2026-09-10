namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents a request to assign a provider call to an agent.
/// </summary>
public sealed class ContactCenterCallAssignmentRequest
{
    /// <summary>
    /// Gets or sets the CRM activity identifier receiving the assignment.
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
    /// Gets or sets the agent identifier receiving the call.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the queue identifier the call is assigned from.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets provider-specific assignment metadata.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}

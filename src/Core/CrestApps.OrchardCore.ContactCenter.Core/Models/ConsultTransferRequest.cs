using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// An agent's request to consult a destination privately before deciding whether to hand the customer over.
/// </summary>
public sealed class ConsultTransferRequest
{
    /// <summary>
    /// Gets or sets the live call the customer is on.
    /// </summary>
    public string CallSessionId { get; set; }

    /// <summary>
    /// Gets or sets the agent placing the consult.
    /// </summary>
    public string InitiatedByAgentId { get; set; }

    /// <summary>
    /// Gets or sets the kind of destination being consulted.
    /// </summary>
    public InteractionTransferTargetType TargetType { get; set; }

    /// <summary>
    /// Gets or sets the destination identifier: an agent id, a queue id, or an approved external entry.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets the resolved address of the destination, already checked against the destination policy.
    /// </summary>
    public string TargetAddress { get; set; }
}

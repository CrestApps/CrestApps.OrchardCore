using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Describes a request to transfer a live interaction to a new destination.
/// </summary>
public sealed class TransferRequest
{
    /// <summary>
    /// Gets or sets the identifier of the interaction being transferred.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the transfer type (blind or consultative).
    /// </summary>
    public InteractionTransferType Type { get; set; }

    /// <summary>
    /// Gets or sets the kind of destination the interaction is transferred to.
    /// </summary>
    public InteractionTransferTargetType TargetType { get; set; }

    /// <summary>
    /// Gets or sets the destination identifier: a queue id, agent id, external address, or entry point id.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent who initiated the transfer.
    /// </summary>
    public string InitiatedByAgentId { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user identifier of the agent who initiated the transfer.
    /// </summary>
    public string InitiatedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the authenticated principal used for transfer-destination RBAC.
    /// </summary>
    public ClaimsPrincipal Principal { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a supervisor is moving another agent's call, authorized against the
    /// supervisor's queue scope rather than against owning the call. The agent on the call is the one released.
    /// </summary>
    public bool SupervisorOperation { get; set; }
}

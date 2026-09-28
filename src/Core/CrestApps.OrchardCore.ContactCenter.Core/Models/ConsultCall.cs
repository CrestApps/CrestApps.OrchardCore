using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a private consult an agent placed before deciding whether to complete a warm transfer.
/// A consult is a first-class part of the topology rather than provider metadata, so a supervisor can
/// see that the customer is held while the agent talks to someone else, and reporting can distinguish a
/// completed warm transfer from an abandoned consult.
/// </summary>
public sealed class ConsultCall
{
    /// <summary>
    /// Gets or sets the platform identifier of the consult.
    /// </summary>
    public string ConsultId { get; set; }

    /// <summary>
    /// Gets or sets the provider identifier of the consult leg.
    /// </summary>
    public string ProviderLegId { get; set; }

    /// <summary>
    /// Gets or sets the agent that placed the consult.
    /// </summary>
    public string InitiatedByAgentId { get; set; }

    /// <summary>
    /// Gets or sets the kind of destination that was consulted.
    /// </summary>
    public InteractionTransferTargetType TargetType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the consulted destination: a queue id, agent id, external address,
    /// or entry point id.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets the resolved address of the consulted destination.
    /// </summary>
    public string TargetAddress { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle state of the consult.
    /// </summary>
    public ConsultCallStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the consult was placed.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the consulting agent and the destination began talking.
    /// </summary>
    public DateTime? ConnectedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the consult ended.
    /// </summary>
    public DateTime? EndedUtc { get; set; }
}

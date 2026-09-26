namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// A live call an agent asked to transfer, already authorized and with its destination resolved.
/// </summary>
public sealed class TransferRoutingContext
{
    /// <summary>
    /// Gets or sets the interaction being transferred.
    /// </summary>
    public Interaction Interaction { get; set; }

    /// <summary>
    /// Gets or sets the call session carrying the interaction.
    /// </summary>
    public CallSession Session { get; set; }

    /// <summary>
    /// Gets or sets the profile identifier of the agent handing the call over.
    /// </summary>
    public string TransferringAgentId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier of the agent handing the call over, which is who the transfer events name.
    /// </summary>
    public string TransferringUserId { get; set; }

    /// <summary>
    /// Gets or sets the resolved destination: an agent profile identifier or a queue identifier.
    /// </summary>
    public string TargetId { get; set; }
}

using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents a call the dialer is placing for the agent, before the agent is connected to it: the customer is
/// ringing, the answer is being screened for an answering machine, or the agent's leg is being joined. The agent's
/// screens show it instead of reading the agent as already on a call.
/// </summary>
public sealed class WorkspacePendingDialViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the interaction of the call.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets where the call stands.
    /// </summary>
    public DialAttemptPhase Phase { get; set; }

    /// <summary>
    /// Gets or sets the customer label, the contact's name when one is known.
    /// </summary>
    public string CustomerLabel { get; set; }

    /// <summary>
    /// Gets or sets the number being dialed.
    /// </summary>
    public string CustomerAddress { get; set; }

    /// <summary>
    /// Gets or sets the display name of the campaign's queue, when known.
    /// </summary>
    public string QueueName { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the call was placed.
    /// </summary>
    public DateTime? StartedUtc { get; set; }
}

using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Everywhere an agent can send the call they are on, as the soft phone's transfer panel lists it.
/// </summary>
internal sealed class SoftPhoneTransferDirectory
{
    /// <summary>
    /// Gets or sets the other agents, with whether each can take a call right now.
    /// </summary>
    public IList<SoftPhoneTransferAgent> Agents { get; set; } = [];

    /// <summary>
    /// Gets or sets the queues the call can be put in.
    /// </summary>
    public IList<SoftPhoneTransferQueue> Queues { get; set; } = [];

    /// <summary>
    /// Gets or sets the approved outside numbers, when the agent may transfer externally.
    /// </summary>
    public IList<SoftPhoneTransferExternalDestination> ExternalDestinations { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the agent may transfer outside the contact center at all.
    /// </summary>
    public bool CanTransferExternally { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the agent may type an outside number that is not on the approved list.
    /// </summary>
    public bool AllowExternalNumbers { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the call's provider can hold the caller while the agent consults,
    /// which is what a warm transfer needs.
    /// </summary>
    public bool SupportsConsult { get; set; }
}

/// <summary>
/// An agent the call can be transferred to.
/// </summary>
internal sealed class SoftPhoneTransferAgent
{
    /// <summary>Gets or sets the agent profile identifier.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the name shown for the agent.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the agent's extension, when they have one.</summary>
    public string Extension { get; set; }

    /// <summary>Gets or sets the agent's presence.</summary>
    public AgentPresenceStatus Presence { get; set; }

    /// <summary>Gets or sets a value indicating whether the agent can take a call right now.</summary>
    public bool Available { get; set; }
}

/// <summary>
/// A queue the call can be transferred to.
/// </summary>
internal sealed class SoftPhoneTransferQueue
{
    /// <summary>Gets or sets the queue identifier.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the queue name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets how many callers are waiting in it.</summary>
    public int Waiting { get; set; }
}

/// <summary>
/// An approved outside number the call can be transferred to.
/// </summary>
internal sealed class SoftPhoneTransferExternalDestination
{
    /// <summary>Gets or sets the catalog identifier the transfer request names.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the name shown for the destination.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the destination's number.</summary>
    public string Number { get; set; }
}

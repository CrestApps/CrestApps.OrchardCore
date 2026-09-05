namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Describes a request to hand an automated (AI-driven) conversation off to a live human agent. It is raised
/// by a channel's automated conversation handler once the AI decides — or the customer asks — to escalate, and
/// consumed by the channel-specific <see cref="Services.IOmnichannelHandoffService"/> implementation that moves
/// the interaction from the automated lane into the human lane.
/// </summary>
public sealed class OmnichannelHandoffRequest
{
    /// <summary>
    /// Gets or sets the automated activity being handed off.
    /// </summary>
    public OmnichannelActivity Activity { get; set; }

    /// <summary>
    /// Gets or sets the target queue the interaction is handed to. When empty, the implementation falls back to
    /// the subject flow's configured handoff queue.
    /// </summary>
    public string TargetQueueId { get; set; }

    /// <summary>
    /// Gets or sets a short reason for the escalation (for example "customer requested a human").
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets an AI-written summary of the conversation so the receiving agent inherits the context.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets the service address (the number we own) the conversation runs on. Used by the SMS handoff
    /// to key the human thread.
    /// </summary>
    public string ServiceAddress { get; set; }

    /// <summary>
    /// Gets or sets the customer address (the contact's number). Used by the SMS handoff to key the human thread.
    /// </summary>
    public string ContactAddress { get; set; }

    /// <summary>
    /// Gets or sets the voice provider technical name of the live call, used by the phone handoff to seat the
    /// call in a queue and bridge it to an agent. Null for non-voice channels.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider call identifier of the live call, used by the phone handoff to bridge the call
    /// to an agent. Null for non-voice channels.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the prior automated transcript, ordered oldest first, so the human handoff can hydrate the
    /// thread the agent inherits. Optional; when empty only the summary is carried across.
    /// </summary>
    public IReadOnlyList<OmnichannelHandoffMessage> Transcript { get; set; }
}

/// <summary>
/// A single message in the automated transcript carried across at handoff. Kept channel-neutral and free of AI
/// types so the human-lane implementations do not depend on the AI packages.
/// </summary>
public sealed class OmnichannelHandoffMessage
{
    /// <summary>
    /// Gets or sets a value indicating whether the message came from the customer (inbound) rather than the
    /// automated agent (outbound).
    /// </summary>
    public bool IsInbound { get; set; }

    /// <summary>
    /// Gets or sets the message text.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the message was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

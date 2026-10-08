namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents a provider request for one phase of an attended (consultative) transfer of a live Contact Center
/// call. The same request shape drives every phase — beginning the consult, completing the handoff, and
/// cancelling it — so the orchestration layer resolves the destination agent once and replays it across phases.
/// </summary>
public sealed class ContactCenterVoiceAttendedTransferRequest
{
    /// <summary>
    /// Gets or sets the interaction identifier.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the provider call identifier of the customer leg whose live conversation is being transferred.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets provider-specific metadata. The destination agent for the consult is carried through the
    /// server-resolved <see cref="ContactCenterConstants.AttendedTransferMetadata.AgentUserId"/> key.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}

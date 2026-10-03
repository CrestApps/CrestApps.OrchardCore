namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The inbound routing of a phone number, stored in its channel endpoint's properties: the entry point that answers
/// calls to the number. The entry point decides the rest (queue or agent, hours, menu and voicemail), exactly as it does
/// for the numbers listed on it.
/// </summary>
public sealed class PhoneEndpointRoutingSettings
{
    /// <summary>
    /// Gets or sets the identifier of the entry point that answers calls to the number, or <see langword="null"/> to
    /// leave the number to the entry points that list it and the queues mapped to it.
    /// </summary>
    public string EntryPointId { get; set; }
}

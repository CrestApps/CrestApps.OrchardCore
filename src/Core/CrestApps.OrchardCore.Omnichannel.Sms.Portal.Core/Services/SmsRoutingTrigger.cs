namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// What caused a conversation to be routed. Ownership used to be decided in three places that had quietly
/// drifted apart; the trigger lets one chain serve all of them without each caller reimplementing the policy.
/// </summary>
public enum SmsRoutingTrigger
{
    /// <summary>
    /// A message arrived from the contact.
    /// </summary>
    Inbound,

    /// <summary>
    /// An automated conversation escalated to a person.
    /// </summary>
    Handoff,

    /// <summary>
    /// A routed thread was not picked up in time and is being placed again.
    /// </summary>
    Reassignment,

    /// <summary>
    /// An agent or supervisor moved the thread to another owner.
    /// </summary>
    ManualTransfer,
}

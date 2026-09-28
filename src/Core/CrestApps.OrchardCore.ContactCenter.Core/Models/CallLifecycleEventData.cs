namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// One state change on a call, a call leg or a queued call: the payload of every call-state event, so reports read
/// the same fields whatever the event.
/// </summary>
/// <remarks>
/// Fields that do not apply to an event are left empty. <see cref="ProviderOccurredUtc"/> is the provider's own time
/// for the change when it reported one, which is what the event is dated by; the event's recorded time is when the
/// platform wrote it.
/// </remarks>
public sealed class CallLifecycleEventData
{
    /// <summary>
    /// Gets or sets the interaction the call belongs to.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the call session.
    /// </summary>
    public string CallSessionId { get; set; }

    /// <summary>
    /// Gets or sets the CRM activity the call belongs to.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the telephony provider.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider's id for the call.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the provider's id for the leg the event is about, for a leg event.
    /// </summary>
    public string ProviderLegId { get; set; }

    /// <summary>
    /// Gets or sets the part the leg plays, such as Customer, Agent or Consult, for a leg event.
    /// </summary>
    public string LegRole { get; set; }

    /// <summary>
    /// Gets or sets the agent on the call or leg.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the queue the call came through.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the campaign the call belongs to, for outbound work.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the call's direction: Inbound or Outbound.
    /// </summary>
    public string Direction { get; set; }

    /// <summary>
    /// Gets or sets the state before the change, when the event is a transition.
    /// </summary>
    public string PreviousState { get; set; }

    /// <summary>
    /// Gets or sets the state after the change.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Gets or sets the normalized hangup cause, for an ending.
    /// </summary>
    public string HangupCause { get; set; }

    /// <summary>
    /// Gets or sets the provider's own hangup cause, unchanged.
    /// </summary>
    public string ProviderHangupCause { get; set; }

    /// <summary>
    /// Gets or sets the SIP response code the provider reported for an ending.
    /// </summary>
    public string SipHangupCause { get; set; }

    /// <summary>
    /// Gets or sets who ended the call, as the provider reported it.
    /// </summary>
    public string HangupSource { get; set; }

    /// <summary>
    /// Gets or sets why the change happened, when the event has a reason: a dial failure, an abandon, a transfer
    /// denial.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets where the call went, for a transfer or consult.
    /// </summary>
    public string Target { get; set; }

    /// <summary>
    /// Gets or sets how long the state that just ended lasted, in seconds: the hold on a resume, the wait on leaving a
    /// queue, the call on an ending.
    /// </summary>
    public double? DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the provider's own time for the change, at the precision it reported, when it reported one.
    /// </summary>
    public DateTime? ProviderOccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets any further detail the event carries, such as an answering-machine verdict or an AI outcome.
    /// </summary>
    public IDictionary<string, string> Details { get; set; } = new Dictionary<string, string>();
}

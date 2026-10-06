namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The payload of an <c>ActivityDispositionApplied</c> event: which activity was completed, with what disposition, by
/// whom, and how to reach the contact, so a workflow can follow up.
/// </summary>
public sealed class ActivityDispositionEventData
{
    /// <summary>
    /// Gets or sets the completed activity.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the disposition the activity was completed with.
    /// </summary>
    public string DispositionId { get; set; }

    /// <summary>
    /// Gets or sets the disposition's name.
    /// </summary>
    public string DispositionName { get; set; }

    /// <summary>
    /// Gets or sets the disposition's outcome, such as <c>NotInService</c> or <c>NoAnswer</c>, or <c>None</c>.
    /// </summary>
    public string Outcome { get; set; }

    /// <summary>
    /// Gets or sets what applied the disposition: <c>Agent</c>, <c>AI</c>, <c>Provider</c>, <c>Workflow</c> or
    /// <c>System</c>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the stable reason the activity ended, when it has one, such as <c>number_not_in_service</c>.
    /// </summary>
    public string TerminalReasonCode { get; set; }

    /// <summary>
    /// Gets or sets the activity's channel, such as <c>Phone</c> or <c>SMS</c>.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the activity's campaign.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the activity's subject content type.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the contact the activity was for.
    /// </summary>
    public string ContactContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the number or address the activity was reaching the contact on.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the attempt number of the activity, counted from one across the chain of activities that tried the
    /// contact again.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets the attempt number of the dialer call the activity was completed after, when the dialer placed one.
    /// </summary>
    public int? AttemptNumber { get; set; }

    /// <summary>
    /// Gets or sets the most attempts the dialer profile allows, when the dialer placed the call.
    /// </summary>
    public int? MaxAttempts { get; set; }

    /// <summary>
    /// Gets or sets how many attempts the dialer profile still allows after this one, when the dialer placed the call.
    /// A workflow that schedules the next call checks it is above zero.
    /// </summary>
    public int? RemainingAttempts { get; set; }

    /// <summary>
    /// Gets or sets how the dialer's call ended, one of <see cref="DialerAttemptOutcomes"/>, when the dialer placed it.
    /// </summary>
    public string DialerOutcome { get; set; }

    /// <summary>
    /// Gets or sets the dialer profile that placed the call, when the dialer placed it.
    /// </summary>
    public string DialerProfileId { get; set; }

    /// <summary>
    /// Gets or sets the interaction of the call the activity was completed after, when there was one.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the user who completed the activity, when a person did.
    /// </summary>
    public string CompletedById { get; set; }
}

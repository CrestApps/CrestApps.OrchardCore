namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The fields of a <c>RoutingDecisionMade</c> event the dialer's activity is counted from.
/// </summary>
/// <remarks>
/// A decision event may stand for several identical decisions folded into one, in which case its payload carries how
/// many as <see cref="Count"/>. A payload without it stands for one decision, so counting with
/// <see cref="Occurrences"/> is right whether or not repeated decisions are folded.
/// </remarks>
internal sealed class DialerRoutingDecisionPayload
{
    /// <summary>
    /// Gets or sets the queue the decision was made for.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the record (CRM activity) the decision was about.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the agent the decision chose.
    /// </summary>
    public string SelectedAgentId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the decision assigned the record to an agent.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets how many identical decisions the event stands for, when it stands for more than one.
    /// </summary>
    public int? Count { get; set; }

    /// <summary>
    /// Gets how many decisions the event stands for: its <see cref="Count"/> when it carries one, otherwise one.
    /// </summary>
    public int Occurrences => Count is > 0 ? Count.Value : 1;
}

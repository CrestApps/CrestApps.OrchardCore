namespace CrestApps.OrchardCore.Omnichannel.Automation;

/// <summary>
/// Kept on a customer's turn in an automated conversation's transcript when the agent handled it on purpose without
/// replying: the agent judged no reply was warranted, or it reached its reply limit. The owed-reply recovery then does
/// not raise the turn again, and the cadence follow-ups treat the agent as waiting on the customer.
/// </summary>
/// <remarks>
/// A mark on the customer's own turn rather than an empty assistant turn, so nothing that reads the transcript (the
/// model, the handoff to the human thread, the conclusion, the reply limit) sees a message that was never sent.
/// </remarks>
internal sealed class AutomatedUnansweredTurn
{
    /// <summary>
    /// Gets or sets why no reply was sent, such as <c>NotWarranted</c> or <c>ReplyLimit</c>.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets when the agent decided not to reply.
    /// </summary>
    public DateTime HandledUtc { get; set; }
}

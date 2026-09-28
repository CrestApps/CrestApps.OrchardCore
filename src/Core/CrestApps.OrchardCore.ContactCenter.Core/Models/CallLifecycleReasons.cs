namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The reasons the call and offer audit records carry, so the writers and the reports built on them agree on each
/// value.
/// </summary>
public static class CallLifecycleReasons
{
    /// <summary>
    /// A call entered a queue to wait for an agent.
    /// </summary>
    public const string Enqueued = "Enqueued";

    /// <summary>
    /// A waiting call was moved to another queue by overflow.
    /// </summary>
    public const string Overflowed = "Overflowed";

    /// <summary>
    /// A waiting call left its queue because an agent accepted it.
    /// </summary>
    public const string Assigned = "Assigned";

    /// <summary>
    /// The call ended while it was still waiting or being offered.
    /// </summary>
    public const string CallEnded = "CallEnded";

    /// <summary>
    /// The caller hung up before any agent answered.
    /// </summary>
    public const string CallerHungUp = "CallerHungUp";

    /// <summary>
    /// The platform withdrew the offer, for example while healing an agent's work state.
    /// </summary>
    public const string Withdrawn = "Withdrawn";

    /// <summary>
    /// The offer was undone because the work it led to could not be started.
    /// </summary>
    public const string Compensated = "Compensated";

    /// <summary>
    /// The offer was released after the work had already been given to a newer offer.
    /// </summary>
    public const string Superseded = "Superseded";

    /// <summary>
    /// The agent accepted the offer but the platform could not connect them to the call.
    /// </summary>
    public const string AnswerFailed = "AnswerFailed";

    /// <summary>
    /// The agent's leg of the call failed to connect.
    /// </summary>
    public const string AgentLegFailed = "AgentLegFailed";

    /// <summary>
    /// An outbound call ended before anybody answered it.
    /// </summary>
    public const string NotAnswered = "NotAnswered";
}

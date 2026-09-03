namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// The outcome of an <see cref="Services.IOmnichannelHandoffService"/> handoff request.
/// </summary>
public sealed class OmnichannelHandoffResult
{
    /// <summary>
    /// Gets a value indicating whether the handoff was accepted and the interaction moved into the human lane.
    /// </summary>
    public bool Succeeded { get; private init; }

    /// <summary>
    /// Gets a human-readable message describing the outcome.
    /// </summary>
    public string Message { get; private init; }

    /// <summary>
    /// Gets the identifier of the human conversation the interaction was routed to, when one was created
    /// (SMS). Null for channels that do not create a conversation record.
    /// </summary>
    public string ConversationId { get; private init; }

    /// <summary>
    /// Gets the identifier of the user the interaction was immediately offered to, when an agent was available
    /// at handoff time (phone). Null when the interaction is waiting in the queue.
    /// </summary>
    public string OfferedToUserId { get; private init; }

    /// <summary>
    /// Gets how the handoff was resolved, so a channel can react (for example a phone channel speaks an
    /// after-hours message and hangs up when a callback was scheduled instead of routing the live call).
    /// </summary>
    public HandoffDisposition Disposition { get; private init; }

    /// <summary>
    /// Creates a successful, routed result (the interaction was moved into the human lane).
    /// </summary>
    /// <param name="message">The outcome message.</param>
    /// <param name="conversationId">The human conversation identifier, when one was created.</param>
    /// <param name="offeredToUserId">The user the interaction was offered to, when applicable.</param>
    public static OmnichannelHandoffResult Success(string message = null, string conversationId = null, string offeredToUserId = null)
        => new() { Succeeded = true, Message = message, ConversationId = conversationId, OfferedToUserId = offeredToUserId, Disposition = HandoffDisposition.Routed };

    /// <summary>
    /// Creates a successful result where, instead of routing the live interaction, a callback was scheduled
    /// (for example the destination queue is closed after hours). The channel should end the interaction with a
    /// suitable message rather than keeping it waiting.
    /// </summary>
    /// <param name="message">The outcome message.</param>
    public static OmnichannelHandoffResult CallbackScheduled(string message = null)
        => new() { Succeeded = true, Message = message, Disposition = HandoffDisposition.CallbackScheduled };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="message">The reason the handoff could not be completed.</param>
    public static OmnichannelHandoffResult Failure(string message)
        => new() { Succeeded = false, Message = message, Disposition = HandoffDisposition.Failed };
}

/// <summary>
/// How an agent handoff was resolved.
/// </summary>
public enum HandoffDisposition
{
    /// <summary>
    /// The interaction was moved into the human lane (a human thread, or a live call seated in a queue).
    /// </summary>
    Routed,

    /// <summary>
    /// The interaction was not routed live; a callback was scheduled instead (for example after hours).
    /// </summary>
    CallbackScheduled,

    /// <summary>
    /// The handoff could not be completed.
    /// </summary>
    Failed,
}

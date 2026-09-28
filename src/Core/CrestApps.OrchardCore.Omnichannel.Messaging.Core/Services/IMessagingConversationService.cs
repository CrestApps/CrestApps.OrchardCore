using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The two-way send path of the messaging workspace, for every channel: an agent replies on (or opens) a
/// conversation, and provider delivery receipts are applied back onto the sent message.
/// </summary>
public interface IMessagingConversationService
{
    /// <summary>
    /// Sends an outbound message on a conversation: authorizes the acting agent, enforces the contact's
    /// opt-out of the channel, sends through the channel, and persists the outbound message.
    /// </summary>
    /// <param name="request">The send request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The send outcome.</returns>
    Task<MessagingSendResult> SendAsync(MessagingSendRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an outbound message on the conversation for an address pair on a channel, creating a personal conversation owned
    /// by the acting agent when none exists (used to start new threads and to fan out a broadcast). Enforces the
    /// contact's opt-out of the channel and sends through it.
    /// </summary>
    /// <param name="channel">The channel to send on.</param>
    /// <param name="serviceAddress">Our address to send from (an endpoint of the channel).</param>
    /// <param name="contactAddress">The recipient's address on the channel.</param>
    /// <param name="body">The message body.</param>
    /// <param name="actingAgentId">The agent the new thread is owned by; null for a system send.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The send outcome.</returns>
    Task<MessagingSendResult> SendDirectAsync(string channel, string serviceAddress, string contactAddress, string body, string actingAgentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a provider delivery receipt to the matching outbound message and notifies the workspace.
    /// </summary>
    /// <param name="receipt">The normalized delivery receipt.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a message was matched and updated.</returns>
    Task<bool> ApplyDeliveryReceiptAsync(MessageDeliveryReceipt receipt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims a pooled or unassigned conversation for the acting agent (claim-to-own), so it disappears from
    /// the other queue members' inboxes.
    /// </summary>
    /// <param name="conversationId">The conversation to claim.</param>
    /// <param name="actingAgentId">The agent claiming the conversation.</param>
    /// <param name="principal">The calling principal, authorized against the conversation. Null for a system call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result: fails when the conversation is already owned by another agent.</returns>
    Task<MessagingSendResult> ClaimAsync(string conversationId, string actingAgentId, ClaimsPrincipal principal = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a conversation to a specific agent (a supervisor action or a transfer).
    /// </summary>
    /// <param name="conversationId">The conversation to assign.</param>
    /// <param name="targetAgentId">The agent to assign the conversation to.</param>
    /// <param name="principal">The calling principal, authorized against the conversation. Null for a system call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    Task<MessagingSendResult> AssignAsync(string conversationId, string targetAgentId, ClaimsPrincipal principal = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the lifecycle status of a conversation (open, snooze, close, or mark spam).
    /// </summary>
    /// <param name="conversationId">The conversation to update.</param>
    /// <param name="status">The new status.</param>
    /// <param name="principal">The calling principal, authorized against the conversation. Null for a system call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    Task<MessagingSendResult> SetStatusAsync(string conversationId, ConversationStatus status, ClaimsPrincipal principal = null, CancellationToken cancellationToken = default);
}

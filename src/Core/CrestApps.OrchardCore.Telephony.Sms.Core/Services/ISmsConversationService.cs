namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

/// <summary>
/// The two-way send path of the SMS portal: an agent replies on (or opens) a conversation, and provider
/// delivery receipts are applied back onto the sent message.
/// </summary>
public interface ISmsConversationService
{
    /// <summary>
    /// Sends an outbound message on a conversation: authorizes the acting agent, enforces the customer's SMS
    /// opt-out, dispatches through the provider that owns the number, and persists the outbound message.
    /// </summary>
    /// <param name="request">The send request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The send outcome.</returns>
    Task<SmsSendResult> SendAsync(SmsSendRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a provider delivery receipt to the matching outbound message and notifies the portal.
    /// </summary>
    /// <param name="receipt">The normalized delivery receipt.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a message was matched and updated.</returns>
    Task<bool> ApplyDeliveryReceiptAsync(SmsDeliveryReceipt receipt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims a pooled or unassigned conversation for the acting agent (claim-to-own), so it disappears from
    /// the other queue members' inboxes.
    /// </summary>
    /// <param name="conversationId">The conversation to claim.</param>
    /// <param name="actingAgentId">The agent claiming the conversation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result: fails when the conversation is already owned by another agent.</returns>
    Task<SmsSendResult> ClaimAsync(string conversationId, string actingAgentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a conversation to a specific agent (a supervisor action or a transfer).
    /// </summary>
    /// <param name="conversationId">The conversation to assign.</param>
    /// <param name="targetAgentId">The agent to assign the conversation to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    Task<SmsSendResult> AssignAsync(string conversationId, string targetAgentId, CancellationToken cancellationToken = default);
}

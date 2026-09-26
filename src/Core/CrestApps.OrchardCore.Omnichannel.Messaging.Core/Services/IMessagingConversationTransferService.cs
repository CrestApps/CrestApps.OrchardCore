using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Hands a conversation to another person, or sends it back to a team's shared pool. It is the same conversation,
/// reassigned, so the whole history stays with it; the transfer itself is recorded in the conversation's history and
/// announced to the workspaces it affects.
/// </summary>
public interface IMessagingConversationTransferService
{
    /// <summary>
    /// Transfers a conversation.
    /// </summary>
    /// <param name="request">What to transfer, where to, and who is asking.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome.</returns>
    Task<MessagingTransferResult> TransferAsync(MessagingTransferRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// A request to transfer a conversation.
/// </summary>
public sealed class MessagingTransferRequest
{
    /// <summary>
    /// Gets or sets the conversation to transfer.
    /// </summary>
    public string ConversationId { get; set; }

    /// <summary>
    /// Gets or sets whether the conversation goes to a person or back to a team.
    /// </summary>
    public ConversationRouteTargetType TargetType { get; set; }

    /// <summary>
    /// Gets or sets the agent profile, or the queue, the conversation goes to.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets an optional internal note for the recipient. It is recorded with the transfer and never sent to
    /// the customer.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets the agent making the transfer.
    /// </summary>
    public string ActingAgentId { get; set; }

    /// <summary>
    /// Gets or sets the principal the transfer is authorized as. Null for a system call.
    /// </summary>
    public ClaimsPrincipal Principal { get; set; }
}

/// <summary>
/// The outcome of a transfer.
/// </summary>
public sealed class MessagingTransferResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the conversation was transferred.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets why the transfer was refused.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the conversation as it stands after the transfer.
    /// </summary>
    public MessagingConversation Conversation { get; set; }

    /// <summary>
    /// Gets or sets the history entry recorded for the transfer.
    /// </summary>
    public MessagingConversationEvent Event { get; set; }

    /// <summary>
    /// Creates a refused result carrying the reason.
    /// </summary>
    public static MessagingTransferResult Failed(string error) => new() { Succeeded = false, Error = error };
}

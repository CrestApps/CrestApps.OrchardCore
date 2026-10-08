namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// The resource passed to <c>IAuthorizationService.AuthorizeAsync</c> when a caller asks to perform an operation
/// on a specific conversation. It carries both the thread and the operation, because the messaging workspace permission
/// (<c>UseMessagingWorkspace</c>) alone does not say which thread the caller may act on.
/// </summary>
public sealed class ConversationAuthorizationResource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationAuthorizationResource"/> class.
    /// </summary>
    /// <param name="conversation">The conversation the caller wants to act on.</param>
    /// <param name="operation">The operation the caller wants to perform.</param>
    public ConversationAuthorizationResource(MessagingConversation conversation, ConversationOperation operation)
    {
        Conversation = conversation;
        Operation = operation;
    }

    /// <summary>
    /// Gets the conversation the caller wants to act on.
    /// </summary>
    public MessagingConversation Conversation { get; }

    /// <summary>
    /// Gets the operation the caller wants to perform.
    /// </summary>
    public ConversationOperation Operation { get; }
}

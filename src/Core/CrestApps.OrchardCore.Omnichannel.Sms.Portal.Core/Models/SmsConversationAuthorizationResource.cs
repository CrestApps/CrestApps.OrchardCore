namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

/// <summary>
/// The resource passed to <c>IAuthorizationService.AuthorizeAsync</c> when a caller asks to perform an operation
/// on a specific SMS conversation. It carries both the thread and the operation, because the SMS portal permission
/// (<c>UseSmsPortal</c>) alone does not say which thread the caller may act on.
/// </summary>
public sealed class SmsConversationAuthorizationResource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsConversationAuthorizationResource"/> class.
    /// </summary>
    /// <param name="conversation">The conversation the caller wants to act on.</param>
    /// <param name="operation">The operation the caller wants to perform.</param>
    public SmsConversationAuthorizationResource(SmsConversation conversation, SmsConversationOperation operation)
    {
        Conversation = conversation;
        Operation = operation;
    }

    /// <summary>
    /// Gets the conversation the caller wants to act on.
    /// </summary>
    public SmsConversation Conversation { get; }

    /// <summary>
    /// Gets the operation the caller wants to perform.
    /// </summary>
    public SmsConversationOperation Operation { get; }
}

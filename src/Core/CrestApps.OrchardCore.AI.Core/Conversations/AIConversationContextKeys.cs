namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// The tool parameter context keys a conversation fills. Keys are lower case, as the binder compares them.
/// </summary>
public static class AIConversationContextKeys
{
    /// <summary>
    /// The conversation's channel.
    /// </summary>
    public const string Channel = "conversation.channel";

    /// <summary>
    /// Whether the customer reached out or we did.
    /// </summary>
    public const string Direction = "conversation.direction";

    /// <summary>
    /// The AI session's id.
    /// </summary>
    public const string SessionId = "conversation.session_id";

    /// <summary>
    /// The omnichannel activity's id.
    /// </summary>
    public const string ActivityId = "activity.id";

    /// <summary>
    /// The contact's content item id.
    /// </summary>
    public const string ContactId = "contact.id";

    /// <summary>
    /// The contact's display name.
    /// </summary>
    public const string ContactName = "contact.name";

    /// <summary>
    /// The customer's phone number or email address.
    /// </summary>
    public const string CustomerAddress = "conversation.customer_address";

    /// <summary>
    /// Our phone number or email address.
    /// </summary>
    public const string BusinessAddress = "conversation.business_address";
}

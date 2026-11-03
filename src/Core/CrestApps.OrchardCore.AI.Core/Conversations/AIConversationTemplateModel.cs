namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// The conversation as Liquid templates see it, under <c>Conversation</c>.
/// </summary>
public sealed class AIConversationTemplateModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationTemplateModel"/> class.
    /// </summary>
    /// <param name="context">The conversation's context.</param>
    public AIConversationTemplateModel(AIConversationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Channel = context.Channel;
        Direction = context.Direction;
        SessionId = context.Session?.SessionId;
        ActivityId = context.ActivityId;
        ContactId = context.Contact?.ContentItemId;
        CustomerAddress = context.CustomerAddress;
        BusinessAddress = context.BusinessAddress;
    }

    /// <summary>
    /// Gets the channel, one of <see cref="AIConversationChannels"/>.
    /// </summary>
    public string Channel { get; }

    /// <summary>
    /// Gets the direction, one of <see cref="AIConversationDirections"/>.
    /// </summary>
    public string Direction { get; }

    /// <summary>
    /// Gets the AI session's id.
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// Gets the omnichannel activity's id, when there is one.
    /// </summary>
    public string ActivityId { get; }

    /// <summary>
    /// Gets the contact's content item id, when the contact is known.
    /// </summary>
    public string ContactId { get; }

    /// <summary>
    /// Gets the customer's phone number or email address.
    /// </summary>
    public string CustomerAddress { get; }

    /// <summary>
    /// Gets our phone number or email address.
    /// </summary>
    public string BusinessAddress { get; }
}

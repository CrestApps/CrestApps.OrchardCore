namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Who started an AI conversation.
/// </summary>
public static class AIConversationDirections
{
    /// <summary>
    /// The customer reached out.
    /// </summary>
    public const string Inbound = "Inbound";

    /// <summary>
    /// The conversation was started for the customer, such as an outbound call or text.
    /// </summary>
    public const string Outbound = "Outbound";
}

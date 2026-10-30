namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// One turn of an AI conversation's transcript.
/// </summary>
/// <param name="Role">Who spoke: <see cref="CustomerRole"/> or <see cref="AssistantRole"/>.</param>
/// <param name="Text">What was said.</param>
/// <param name="CreatedUtc">When it was said.</param>
public sealed record AIConversationTurn(string Role, string Text, DateTime CreatedUtc)
{
    /// <summary>
    /// The role of the customer's turns.
    /// </summary>
    public const string CustomerRole = "Customer";

    /// <summary>
    /// The role of the AI's turns.
    /// </summary>
    public const string AssistantRole = "Assistant";
}

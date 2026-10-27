namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// The input every AI conversation workflow event passes, so a workflow reads the same names whichever event
/// started it, for example <c>{{ Workflow.Input.CustomerAddress }}</c> or <c>{{ Workflow.Input.Variables.tier }}</c>.
/// </summary>
public static class AIConversationWorkflowInput
{
    /// <summary>
    /// The <see cref="AIConversationStartingResult"/> a starting workflow writes its decisions to.
    /// </summary>
    public const string StartingResult = "StartingResult";

    /// <summary>
    /// The arguments a tool workflow was called with.
    /// </summary>
    public const string Arguments = "Arguments";

    /// <summary>
    /// Builds the input that describes a conversation.
    /// </summary>
    /// <param name="conversation">The conversation, or <see langword="null"/> outside one.</param>
    /// <returns>The input by name.</returns>
    public static Dictionary<string, object> Build(AIConversationContext conversation)
    {
        var input = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (conversation is null)
        {
            input["Variables"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            return input;
        }

        input["Channel"] = conversation.Channel;
        input["Direction"] = conversation.Direction;
        input["SessionId"] = conversation.Session?.SessionId;
        input["ProfileId"] = conversation.Profile?.ItemId ?? conversation.Session?.ProfileId;
        input["ActivityId"] = conversation.ActivityId;
        input["ContactId"] = conversation.Contact?.ContentItemId;
        input["ContactName"] = conversation.Contact?.DisplayText;
        input["Contact"] = conversation.Contact;
        input["CustomerAddress"] = conversation.CustomerAddress;
        input["BusinessAddress"] = conversation.BusinessAddress;
        input["Variables"] = new Dictionary<string, string>(conversation.GetVariables(), StringComparer.OrdinalIgnoreCase);

        return input;
    }
}

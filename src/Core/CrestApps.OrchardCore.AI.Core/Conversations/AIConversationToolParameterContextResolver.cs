using CrestApps.Core.AI.Tooling.Parameters;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Fills tool parameters from the current conversation, so a tool can be given the caller's number, the contact or
/// a conversation variable without the model typing it.
/// </summary>
/// <remarks>
/// Any variable can be read with <c>variables.</c> followed by its name, such as <c>variables.account_id</c>; the
/// listed keys are the ones every conversation has.
/// </remarks>
public sealed class AIConversationToolParameterContextResolver : IAIToolParameterContextResolver
{
    /// <summary>
    /// The prefix that reads a conversation variable.
    /// </summary>
    public const string VariablePrefix = "variables.";

    private static readonly AIToolParameterContextKey[] _supportedKeys =
    [
        new(AIConversationContextKeys.Channel, "Conversation channel", "Chat, Sms, Phone or Email."),
        new(AIConversationContextKeys.Direction, "Conversation direction", "Inbound when the customer reached out, Outbound when we did."),
        new(AIConversationContextKeys.SessionId, "Conversation session ID", "The AI session's identifier."),
        new(AIConversationContextKeys.ActivityId, "Activity ID", "The omnichannel activity the conversation belongs to."),
        new(AIConversationContextKeys.ContactId, "Contact ID", "The content item id of the contact the conversation is with."),
        new(AIConversationContextKeys.ContactName, "Contact name", "The display name of the contact the conversation is with."),
        new(AIConversationContextKeys.CustomerAddress, "Customer phone or email", "The phone number or email address of the customer."),
        new(AIConversationContextKeys.BusinessAddress, "Our phone or email", "The phone number or email address the conversation uses on our side."),
    ];

    /// <inheritdoc/>
    public IReadOnlyList<AIToolParameterContextKey> SupportedKeys => _supportedKeys;

    /// <inheritdoc/>
    public bool TryResolve(string contextKey, IServiceProvider services, out object value)
    {
        value = null;

        var conversation = AIConversationScope.Resolve();

        if (conversation is null || string.IsNullOrEmpty(contextKey))
        {
            return false;
        }

        if (contextKey.StartsWith(VariablePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var name = contextKey[VariablePrefix.Length..];

            if (conversation.GetVariables().TryGetValue(name, out var variable))
            {
                value = variable;
            }

            return value is not null;
        }

        value = contextKey.ToLowerInvariant() switch
        {
            AIConversationContextKeys.Channel => conversation.Channel,
            AIConversationContextKeys.Direction => conversation.Direction,
            AIConversationContextKeys.SessionId => conversation.Session?.SessionId,
            AIConversationContextKeys.ActivityId => conversation.ActivityId,
            AIConversationContextKeys.ContactId => conversation.Contact?.ContentItemId,
            AIConversationContextKeys.ContactName => conversation.Contact?.DisplayText,
            AIConversationContextKeys.CustomerAddress => conversation.CustomerAddress,
            AIConversationContextKeys.BusinessAddress => conversation.BusinessAddress,
            _ => null,
        };

        return value is not null;
    }
}

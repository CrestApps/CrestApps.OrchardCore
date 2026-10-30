using CrestApps.Core.AI.Completions;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// The usage purposes this host records for its own features, beside the framework's
/// <see cref="AIUsagePurposes"/>.
/// </summary>
public static class AIUsageFeaturePurposes
{
    /// <summary>
    /// Reviewing a finished conversation to choose its disposition and the follow-up actions.
    /// </summary>
    public const string ConversationConclusion = "ConversationConclusion";

    /// <summary>
    /// Summarizing a conversation for the agent it is handed to.
    /// </summary>
    public const string HandoffSummary = "HandoffSummary";

    /// <summary>
    /// Deciding whether an incoming message needs a reply.
    /// </summary>
    public const string ReplyDecision = "ReplyDecision";

    /// <summary>
    /// Writing a message that re-engages a contact who stopped replying.
    /// </summary>
    public const string ReEngagement = "ReEngagement";

    /// <summary>
    /// A one-off completion requested through the utility completion API.
    /// </summary>
    public const string Utility = "Utility";

    /// <summary>
    /// Rewriting an AI Profile Builder session's instructions from the author's feedback.
    /// </summary>
    public const string ProfileBuilderCoaching = "ProfileBuilderCoaching";

    /// <summary>
    /// Replaying and judging the replies an AI Profile Builder author gave feedback on.
    /// </summary>
    public const string ProfileBuilderReplay = "ProfileBuilderReplay";

    /// <summary>
    /// Scoring a conversation against its quality criteria.
    /// </summary>
    public const string ConversationEvaluation = "ConversationEvaluation";

    /// <summary>
    /// Playing the customer in a simulated test conversation.
    /// </summary>
    public const string ConversationSimulation = "ConversationSimulation";

    /// <summary>
    /// Rewriting what the AI remembers about a contact after a conversation.
    /// </summary>
    public const string ContactMemory = "ContactMemory";
}

using CrestApps.Core.AI.Completions;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// The usage categories this host records for features other than chat sessions and chat interactions, so the AI
/// usage report can show what each feature spent. They label requests through <see cref="AIUsageScope"/>.
/// </summary>
public static class AIUsageCategories
{
    /// <summary>
    /// Automated SMS conversations.
    /// </summary>
    public const string Sms = "Sms";

    /// <summary>
    /// Automated voice calls.
    /// </summary>
    public const string Voice = "Voice";

    /// <summary>
    /// Requests made through the AI completion API endpoints.
    /// </summary>
    public const string Api = "Api";

    /// <summary>
    /// Requests made by workflow tasks.
    /// </summary>
    public const string Workflow = "Workflow";

    /// <summary>
    /// Tasks received from other agents over the Agent-to-Agent protocol.
    /// </summary>
    public const string AgentToAgent = "A2A";
}

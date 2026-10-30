using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// The channel and profile filters the AI conversation events share.
/// </summary>
internal static class AIConversationEventFilter
{
    /// <summary>
    /// Whether the conversation in the workflow's input matches an event's filters. An empty filter matches all.
    /// </summary>
    /// <param name="workflowContext">The workflow context whose input describes the conversation.</param>
    /// <param name="channel">The channel filter.</param>
    /// <param name="profileId">The profile filter.</param>
    /// <returns><see langword="true"/> when the conversation matches.</returns>
    public static bool Matches(WorkflowExecutionContext workflowContext, string channel, string profileId)
        => Matches(workflowContext.Input, "Channel", channel) && Matches(workflowContext.Input, "ProfileId", profileId);

    private static bool Matches(IDictionary<string, object> input, string key, string expected)
        => string.IsNullOrWhiteSpace(expected) ||
            (input.TryGetValue(key, out var value) && string.Equals(value?.ToString(), expected, StringComparison.OrdinalIgnoreCase));
}

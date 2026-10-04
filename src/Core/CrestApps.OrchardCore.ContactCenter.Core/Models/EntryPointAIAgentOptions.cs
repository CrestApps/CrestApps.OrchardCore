namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Holds the entry point channels whose traffic an AI agent can answer, registered by the features that let it: texts
/// by SMS Omnichannel Automation. Calls are offered an AI voice agent when a provider's AI voice answerer is registered.
/// </summary>
public sealed class EntryPointAIAgentOptions
{
    /// <summary>
    /// Gets the channels an entry point may route to an AI agent (case-insensitive).
    /// </summary>
    public HashSet<string> Channels { get; } = new(StringComparer.OrdinalIgnoreCase);
}

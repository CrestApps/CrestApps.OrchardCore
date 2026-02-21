namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Global site settings for the default orchestrator.
/// Configurable under Settings >> Artificial Intelligence.
/// </summary>
public sealed class DefaultOrchestratorSettings
{
    /// <summary>
    /// Gets or sets whether Preemptive RAG is enabled globally.
    /// When enabled, the user's query is rewritten into focused search terms and used
    /// to retrieve relevant context from data sources and documents before the LLM call.
    /// </summary>
    public bool EnablePreemptiveRag { get; set; } = true;
}

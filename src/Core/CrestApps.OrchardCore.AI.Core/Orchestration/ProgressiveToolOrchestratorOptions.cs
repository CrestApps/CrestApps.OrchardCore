namespace CrestApps.OrchardCore.AI.Core.Orchestration;

/// <summary>
/// Configuration options for the progressive tool orchestrator.
/// </summary>
public sealed class ProgressiveToolOrchestratorOptions
{
    /// <summary>
    /// Gets or sets the maximum number of tools to include in the initial scoped set
    /// when scoping is active. Default is 10.
    /// </summary>
    public int InitialToolCount { get; set; } = 10;

    /// <summary>
    /// Gets or sets the tool count threshold below which all tools are passed directly
    /// without any scoping or planning. Default is 30.
    /// </summary>
    public int ScopingThreshold { get; set; } = 30;

    /// <summary>
    /// Gets or sets the tool count threshold above which the full LLM planning phase
    /// is activated. Between <see cref="ScopingThreshold"/> and this value, lightweight
    /// token-based scoping is used without an LLM call.
    /// The planner is also activated when MCP tools are present, regardless of this threshold.
    /// Default is 100.
    /// </summary>
    public int PlanningThreshold { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of progressive expansion rounds.
    /// Each round adds more tools to the scoped set if a capability gap is detected.
    /// Default is 3.
    /// </summary>
    public int MaxExpansionRounds { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of tools that can be injected
    /// after all expansion rounds. Default is 30.
    /// </summary>
    public int MaxToolCount { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum number of recent conversation messages to include
    /// in the planning phase for context. This allows the planner to understand
    /// follow-up requests (e.g., "yes", "do it", "also send a notification").
    /// Only user and assistant text messages are included; tool call details are excluded.
    /// Default is 10.
    /// </summary>
    public int PlanningHistoryMessageCount { get; set; } = 10;
}

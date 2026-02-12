using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Defines a pluggable orchestration runtime responsible for planning, tool scoping,
/// and managing the execution loop for an AI completion session.
/// </summary>
/// <remarks>
/// <para>Each chat session binds to exactly one orchestrator runtime for its lifetime.
/// The orchestrator is resolved per session based on the AI profile or chat interaction configuration.</para>
/// <para>Implementations should handle:
/// <list type="bullet">
///   <item>Planning the task (decomposing complex requests into steps)</item>
///   <item>Scoping relevant tools (selecting a subset of available tools)</item>
///   <item>Executing iterative agent loops (calling the LLM with scoped tools)</item>
///   <item>Detecting capability gaps and expanding the tool scope progressively</item>
///   <item>Producing the final streaming response</item>
/// </list></para>
/// </remarks>
public interface IOrchestrator
{
    /// <summary>
    /// Gets the unique name of this orchestrator.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the orchestration pipeline and yields streaming completion updates.
    /// </summary>
    /// <param name="context">The orchestration context containing the user message,
    /// conversation history, completion settings, document references, and extensible properties.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An asynchronous stream of completion updates.</returns>
    IAsyncEnumerable<ChatResponseUpdate> ExecuteStreamingAsync(
        OrchestrationContext context,
        CancellationToken cancellationToken = default);
}

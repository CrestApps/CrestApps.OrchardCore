using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Defines a handler that processes preemptive RAG (Retrieval-Augmented Generation) for a specific
/// data source type. Implementations receive pre-extracted search queries and are responsible for
/// embedding them, performing vector search, and injecting relevant context into the system message.
/// </summary>
public interface IPreemptiveRagHandler
{
    /// <summary>
    /// Determines asynchronously whether the specified context can be handled by this instance.
    /// </summary>
    /// <param name="context">The context to evaluate for handling. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the context can
    /// be handled; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> CanHandleAsync(OrchestrationContextBuiltContext context);

    /// <summary>
    /// Handles preemptive RAG injection for the given context and search queries.
    /// </summary>
    /// <param name="context">The preemptive RAG context containing the orchestration state and extracted queries.</param>
    Task HandleAsync(PreemptiveRagContext context);
}

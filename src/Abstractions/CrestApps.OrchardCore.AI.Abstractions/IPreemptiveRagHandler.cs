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
    /// Handles preemptive RAG injection for the given context and search queries.
    /// </summary>
    /// <param name="context">The preemptive RAG context containing the orchestration state and extracted queries.</param>
    Task HandleAsync(PreemptiveRagContext context);
}

namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Carries the state needed by <see cref="IPreemptiveRagHandler"/> implementations
/// during a preemptive RAG pass.
/// </summary>
public sealed class PreemptiveRagContext
{
    public PreemptiveRagContext(OrchestrationContext orchestrationContext, object resource, IList<string> queries)
    {
        ArgumentNullException.ThrowIfNull(orchestrationContext);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(queries);

        OrchestrationContext = orchestrationContext;
        Resource = resource;
        Queries = queries;
    }

    /// <summary>
    /// Gets the current orchestration context, including the system message builder and completion context.
    /// </summary>
    public OrchestrationContext OrchestrationContext { get; }

    /// <summary>
    /// Gets the source resource (e.g., <c>AIProfile</c> or <c>ChatInteraction</c>).
    /// </summary>
    public object Resource { get; }

    /// <summary>
    /// Gets the focused search queries extracted by the preemptive search query provider.
    /// </summary>
    public IList<string> Queries { get; }
}

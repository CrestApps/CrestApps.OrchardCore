using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Per-invocation context for AI operations, providing isolation between concurrent
/// SignalR hub method calls. Each hub invocation creates its own instance via
/// <see cref="AIInvocationScope.Begin"/>, and AI tools retrieve it via
/// <see cref="AIInvocationScope.Current"/>.
///
/// <para>
/// <b>Why this exists:</b> In SignalR, <c>HttpContext</c> is shared across all hub
/// method invocations on the same WebSocket connection. If a user sends multiple
/// messages concurrently, writing to <c>HttpContext.Items</c> causes data leaks
/// between invocations. This class uses <see cref="System.Threading.AsyncLocal{T}"/>
/// (via <see cref="AIInvocationScope"/>) to provide truly invocation-scoped storage
/// that flows correctly through async/await chains without any cross-invocation
/// contamination.
/// </para>
///
/// <para>
/// <b>For AI tools:</b> Tools are registered as singletons and the AI model does not
/// pass any invocation identifier when calling them. Tools retrieve the current
/// invocation context by calling <c>AIInvocationScope.Current</c>, which returns the
/// context for the async execution flow that is calling the tool — even when multiple
/// invocations are in flight simultaneously on different threads or continuations.
/// </para>
/// </summary>
public sealed class AIInvocationContext
{
    private int _referenceIndex;

    /// <summary>
    /// Gets or sets the <see cref="AIToolExecutionContext"/> for the current invocation,
    /// providing provider, connection, and resource information.
    /// </summary>
    public AIToolExecutionContext ToolExecutionContext { get; set; }

    /// <summary>
    /// Gets or sets the data source identifier for the current invocation.
    /// Used by <c>DataSourceSearchTool</c> to scope searches to the correct data source.
    /// </summary>
    public string DataSourceId { get; set; }

    /// <summary>
    /// Gets the dictionary of citation references collected during tool execution
    /// (e.g., from <c>DataSourceSearchTool</c> and <c>SearchDocumentsTool</c>).
    /// Keyed by the citation marker (e.g., "[doc:1]") with the reference metadata as value.
    /// </summary>
    public Dictionary<string, AICompletionReference> ToolReferences { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a general-purpose property bag for extensibility.
    /// Handlers and tools can store arbitrary per-invocation data here.
    /// </summary>
    public Dictionary<string, object> Items { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the next unique reference index for citation markers (e.g., [doc:1], [doc:2], ...).
    /// This method is thread-safe and ensures a monotonically increasing counter across all
    /// handlers and tools within the same invocation, preventing index collisions between
    /// <c>DataSourcePreemptiveRagHandler</c>, <c>DocumentPreemptiveRagHandler</c>,
    /// <c>DataSourceSearchTool</c>, and <c>SearchDocumentsTool</c>.
    /// </summary>
    /// <returns>The next 1-based reference index.</returns>
    public int NextReferenceIndex()
        => Interlocked.Increment(ref _referenceIndex);
}

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Manages an <see cref="AsyncLocal{T}"/>-backed ambient context for AI invocations.
/// Each SignalR hub method call (or any entry point) creates a scope with
/// <see cref="Begin"/>, and any code running in that async flow can access the
/// context via <see cref="Current"/>.
///
/// <para>
/// <b>Usage in hubs:</b>
/// <code>
/// using var scope = AIInvocationScope.Begin();
/// // scope.Context is the AIInvocationContext for this invocation.
/// // All downstream code (handlers, tools) can call AIInvocationScope.Current.
/// </code>
/// </para>
///
/// <para>
/// <b>Usage in AI tools (singletons):</b>
/// <code>
/// var context = AIInvocationScope.Current;
/// var dataSourceId = context?.DataSourceId;
/// </code>
/// Because <see cref="AsyncLocal{T}"/> flows through async/await continuations,
/// each concurrent invocation sees its own context — even when multiple invocations
/// share the same <c>HttpContext</c> in a SignalR WebSocket connection.
/// </para>
/// </summary>
public static class AIInvocationScope
{
    private static readonly AsyncLocal<AIInvocationContext> _current = new();

    /// <summary>
    /// Gets the <see cref="AIInvocationContext"/> for the current async execution flow,
    /// or <c>null</c> if no scope has been started.
    /// </summary>
    public static AIInvocationContext Current => _current.Value;

    /// <summary>
    /// Begins a new invocation scope with a fresh <see cref="AIInvocationContext"/>.
    /// The returned <see cref="Scope"/> must be disposed to clear the context.
    /// </summary>
    /// <returns>A disposable scope that clears the context on disposal.</returns>
    public static Scope Begin()
        => new(new AIInvocationContext());

    /// <summary>
    /// Begins a new invocation scope with the specified <see cref="AIInvocationContext"/>.
    /// The returned <see cref="Scope"/> must be disposed to clear the context.
    /// </summary>
    /// <param name="context">The invocation context to make current.</param>
    /// <returns>A disposable scope that clears the context on disposal.</returns>
    public static Scope Begin(AIInvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new Scope(context);
    }

    /// <summary>
    /// A disposable wrapper that sets and clears the <see cref="AIInvocationScope.Current"/> context.
    /// </summary>
    public readonly struct Scope : IDisposable
    {
        /// <summary>
        /// Gets the <see cref="AIInvocationContext"/> associated with this scope.
        /// </summary>
        public AIInvocationContext Context { get; }

        internal Scope(AIInvocationContext context)
        {
            Context = context;
            _current.Value = context;
        }

        /// <summary>
        /// Clears the current invocation context, preventing data from leaking
        /// into subsequent operations on the same thread.
        /// </summary>
        public void Dispose()
        {
            _current.Value = null;
        }
    }
}

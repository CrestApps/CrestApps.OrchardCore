namespace CrestApps.Core.Hosting;

/// <summary>
/// Runs work in its own dependency-injection scope, so a caller can reach a fresh set of scoped
/// services without knowing how the host builds them.
/// </summary>
public interface IScopedWorkExecutor
{
    /// <summary>
    /// Runs an operation in a new scope, resolving the context it asks for from that scope.
    /// </summary>
    /// <typeparam name="TContext">The scoped context type.</typeparam>
    /// <param name="operation">The operation to run.</param>
    Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
        where TContext : notnull;

    /// <summary>
    /// Runs an operation in a new scope, handing it that scope's service provider.
    /// </summary>
    /// <param name="operation">The operation to run.</param>
    Task ExecuteAsync(Func<IServiceProvider, Task> operation);
}

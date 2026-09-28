namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Executes Contact Center operations in isolated Orchard shell scopes and schedules typed work after commit.
/// </summary>
public interface IContactCenterScopeExecutor
{
    /// <summary>
    /// Executes an operation in a new child scope using the requested scoped context.
    /// </summary>
    /// <typeparam name="TContext">The scoped context type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
        where TContext : notnull;

    /// <summary>
    /// Executes an operation in a new child scope, handing it that scope's service provider.
    /// </summary>
    /// <remarks>
    /// For work that needs several collaborators from the same scope, and for work that has to reach a scope
    /// boundary to make progress: anything scheduled with <see cref="ScheduleAfterCommit(Func{Task})"/> runs when
    /// the scope commits, so a long-running loop that stays in one scope holds all of it until the loop ends.
    /// </remarks>
    /// <param name="operation">The operation to execute, given the child scope's service provider.</param>
    Task ExecuteAsync(Func<IServiceProvider, Task> operation);

    /// <summary>
    /// Schedules an operation to execute after the current Orchard shell scope commits.
    /// </summary>
    /// <typeparam name="TContext">The scoped context type.</typeparam>
    /// <param name="operation">The operation to execute after commit.</param>
    /// <returns><see langword="true"/> when the operation was scheduled; otherwise, <see langword="false"/>.</returns>
    bool ScheduleAfterCommit<TContext>(Func<TContext, Task> operation)
        where TContext : notnull;

    /// <summary>
    /// Schedules a captured operation to execute after the current Orchard shell scope commits.
    /// </summary>
    /// <param name="operation">The operation to execute after commit.</param>
    /// <returns><see langword="true"/> when the operation was scheduled; otherwise, <see langword="false"/>.</returns>
    bool ScheduleAfterCommit(Func<Task> operation);
}

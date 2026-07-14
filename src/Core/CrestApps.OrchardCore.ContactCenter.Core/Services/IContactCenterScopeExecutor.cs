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

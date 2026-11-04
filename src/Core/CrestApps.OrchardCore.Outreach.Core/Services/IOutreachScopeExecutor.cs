namespace CrestApps.OrchardCore.Outreach.Core.Services;

/// <summary>
/// Runs work in an isolated scope with a session of its own, so one batch's failure never breaks the next.
/// </summary>
public interface IOutreachScopeExecutor
{
    /// <summary>
    /// Runs the operation in a new scope.
    /// </summary>
    /// <param name="operation">The operation, given the new scope's services.</param>
    Task ExecuteAsync(Func<IServiceProvider, Task> operation);
}

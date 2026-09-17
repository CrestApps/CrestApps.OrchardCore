using CrestApps.Core.Hosting;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Core.Hosting;

/// <summary>
/// Hands after-commit work to Orchard Core's shell scope, which already defers it until the scope
/// commits.
/// </summary>
/// <remarks>
/// Because the shell scope owns the deferred work once it is handed over, this queue never holds
/// anything itself: <see cref="HasPendingWork"/> is always <see langword="false"/> and
/// <see cref="DrainAsync"/> does nothing. Work enqueued outside a shell scope is dropped, which is
/// what the callers did before this seam existed.
/// </remarks>
public sealed class ShellScopeAfterCommitTaskQueue : IAfterCommitTaskQueue
{
    /// <inheritdoc/>
    public bool HasPendingWork => false;

    /// <inheritdoc/>
    public void Enqueue(Func<IServiceProvider, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (ShellScope.Current is null)
        {
            return;
        }

        ShellScope.AddDeferredTask(scope => operation(scope.ServiceProvider));
    }

    /// <inheritdoc/>
    public Task DrainAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

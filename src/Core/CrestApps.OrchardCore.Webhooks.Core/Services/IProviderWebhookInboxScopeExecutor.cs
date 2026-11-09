namespace CrestApps.OrchardCore.Webhooks.Core.Services;

/// <summary>
/// Runs inbox work in a fresh tenant scope, with its own YesSql session, so one delivery's failure or concurrency loss
/// never reaches the next one.
/// </summary>
public interface IProviderWebhookInboxScopeExecutor
{
    /// <summary>
    /// Runs an operation against the inbox resolved from a fresh child scope, and commits the scope when it completes.
    /// </summary>
    /// <param name="operation">The operation to run.</param>
    /// <returns>A task that completes when the operation and its scope have completed.</returns>
    Task ExecuteAsync(Func<IProviderWebhookInbox, Task> operation);
}

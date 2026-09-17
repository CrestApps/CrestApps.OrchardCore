namespace CrestApps.Core.Hosting;

/// <summary>
/// Starts work on a scope that outlives the caller.
/// </summary>
/// <remarks>
/// A provider gives a webhook seconds to answer, and some sessions hold one open far longer than
/// that. By the time such a session ends, the request has timed out, been retried and had its
/// connection aborted, so anything still attached to it is gone. Work handed to this executor is
/// deliberately not awaited by the caller and does not belong to the caller's scope.
/// </remarks>
public interface IDetachedWorkExecutor
{
    /// <summary>
    /// Starts the operation on its own scope and returns immediately.
    /// </summary>
    /// <param name="operation">The operation to run. It is responsible for handling its own failures.</param>
    void Run(Func<IServiceProvider, Task> operation);
}

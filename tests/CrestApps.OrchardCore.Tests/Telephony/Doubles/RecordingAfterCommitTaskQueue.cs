using CrestApps.Core.Hosting;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// Keeps after-commit work so a test can decide when, or whether, it runs.
/// </summary>
/// <remarks>
/// Nothing runs on enqueue. A test that wants the deferred work to happen calls
/// <see cref="DrainAsync"/>; one that wants to prove the work was deferred rather than done inline
/// simply inspects <see cref="HasPendingWork"/>.
/// </remarks>
internal sealed class RecordingAfterCommitTaskQueue : IAfterCommitTaskQueue
{
    private readonly List<Func<IServiceProvider, Task>> _operations = [];

    public bool HasPendingWork => _operations.Count > 0;

    public int EnqueuedCount => _operations.Count;

    public void Enqueue(Func<IServiceProvider, Task> operation)
        => _operations.Add(operation);

    public async Task DrainAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var pending = _operations.ToArray();
        _operations.Clear();

        foreach (var operation in pending)
        {
            await operation(serviceProvider);
        }
    }
}

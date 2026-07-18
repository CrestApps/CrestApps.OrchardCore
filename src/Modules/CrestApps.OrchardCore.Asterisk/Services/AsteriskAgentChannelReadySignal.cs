using System.Collections.Concurrent;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Tenant-scoped, in-memory coordinator that lets a connect operation wait for a module-originated agent
/// channel to enter the Stasis application. Registered as a tenant singleton so its waiter map is isolated to
/// the current tenant; it holds no static state and therefore never shares data across tenants.
/// </summary>
internal sealed class AsteriskAgentChannelReadySignal : IAsteriskAgentChannelReadySignal
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _waiters =
        new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public IAsteriskAgentChannelReadyRegistration Register(string channelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // A deterministic channel id can only have one in-flight connect attempt. If a stale registration is
        // still present, release it as not-ready so it can never block a superseding attempt indefinitely.
        _waiters.AddOrUpdate(
            channelId,
            completion,
            (_, existing) =>
            {
                existing.TrySetResult(false);

                return completion;
            });

        return new Registration(this, channelId, completion);
    }

    /// <inheritdoc/>
    public void Signal(string channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return;
        }

        if (_waiters.TryGetValue(channelId, out var completion))
        {
            completion.TrySetResult(true);
        }
    }

    private void Remove(string channelId, TaskCompletionSource<bool> completion)
    {
        // Only remove the entry when it is still the registration we created, so a newer waiter for the same
        // deterministic id is never torn down by an older registration's disposal.
        _waiters.TryRemove(new KeyValuePair<string, TaskCompletionSource<bool>>(channelId, completion));
    }

    private sealed class Registration : IAsteriskAgentChannelReadyRegistration
    {
        private readonly AsteriskAgentChannelReadySignal _owner;
        private readonly string _channelId;
        private readonly TaskCompletionSource<bool> _completion;

        public Registration(
            AsteriskAgentChannelReadySignal owner,
            string channelId,
            TaskCompletionSource<bool> completion)
        {
            _owner = owner;
            _channelId = channelId;
            _completion = completion;
        }

        public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                var completed = await Task.WhenAny(_completion.Task, Task.Delay(timeout, delayCancellation.Token));

                if (completed == _completion.Task)
                {
                    return _completion.Task.Result;
                }

                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                delayCancellation.Cancel();
            }
        }

        public void Dispose()
        {
            _completion.TrySetResult(false);
            _owner.Remove(_channelId, _completion);
        }
    }
}

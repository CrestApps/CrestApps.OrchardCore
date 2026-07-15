using System.Collections.Concurrent;

namespace CrestApps.OrchardCore.ContactCenter.Services;

internal sealed class ContactCenterFeatureWorkManager : IContactCenterFeatureWorkManager
{
    private readonly ConcurrentDictionary<string, FeatureWorkState> _states = new(StringComparer.Ordinal);

    public IContactCenterFeatureWorkLease TryEnter(string featureId)
    {
        ArgumentException.ThrowIfNullOrEmpty(featureId);

        var state = _states.GetOrAdd(featureId, static _ => new FeatureWorkState());

        lock (state.SyncRoot)
        {
            if (state.IsQuiescing)
            {
                return null;
            }

            if (state.ActiveWorkCount == 0)
            {
                state.Drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            state.ActiveWorkCount++;

            return new FeatureWorkLease(state);
        }
    }

    public void Quiesce(string featureId)
    {
        ArgumentException.ThrowIfNullOrEmpty(featureId);

        var state = _states.GetOrAdd(featureId, static _ => new FeatureWorkState());

        lock (state.SyncRoot)
        {
            state.IsQuiescing = true;

            if (state.ActiveWorkCount == 0)
            {
                state.Drained.TrySetResult();
            }
        }
    }

    public async Task DrainAsync(
        string featureId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(featureId);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "The feature drain timeout must be greater than zero.");
        }

        var state = _states.GetOrAdd(featureId, static _ => new FeatureWorkState());
        Task drained;

        lock (state.SyncRoot)
        {
            drained = state.ActiveWorkCount == 0
                ? Task.CompletedTask
                : state.Drained.Task;
        }

        await drained.WaitAsync(timeout, cancellationToken);
    }

    public void Activate(string featureId)
    {
        ArgumentException.ThrowIfNullOrEmpty(featureId);

        var state = _states.GetOrAdd(featureId, static _ => new FeatureWorkState());

        lock (state.SyncRoot)
        {
            state.IsQuiescing = false;
        }
    }

    private sealed class FeatureWorkState
    {
        public Lock SyncRoot { get; } = new();

        public TaskCompletionSource Drained { get; set; } = CreateCompletedSource();

        public int ActiveWorkCount { get; set; }

        public bool IsQuiescing { get; set; }

        private static TaskCompletionSource CreateCompletedSource()
        {
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            source.SetResult();

            return source;
        }
    }

    private sealed class FeatureWorkLease : IContactCenterFeatureWorkLease
    {
        private FeatureWorkState _state;

        public FeatureWorkLease(FeatureWorkState state)
        {
            _state = state;
        }

        public void Dispose()
        {
            var state = Interlocked.Exchange(ref _state, null);

            if (state is null)
            {
                return;
            }

            lock (state.SyncRoot)
            {
                state.ActiveWorkCount--;

                if (state.ActiveWorkCount == 0)
                {
                    state.Drained.TrySetResult();
                }
            }
        }
    }
}

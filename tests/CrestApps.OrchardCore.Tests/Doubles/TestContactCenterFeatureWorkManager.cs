using CrestApps.OrchardCore.ContactCenter;

namespace CrestApps.OrchardCore.Tests.Doubles;

internal sealed class TestContactCenterFeatureWorkManager : IContactCenterFeatureWorkManager
{
    private readonly HashSet<string> _quiescingFeatures = new(StringComparer.Ordinal);
    private int _activeLeaseCount;

    public int ActiveLeaseCount => Volatile.Read(ref _activeLeaseCount);

    public IContactCenterFeatureWorkLease TryEnter(string featureId)
    {
        return _quiescingFeatures.Contains(featureId)
            ? null
            : new TestContactCenterFeatureWorkLease(this);
    }

    public void Quiesce(string featureId)
    {
        _quiescingFeatures.Add(featureId);
    }

    public Task DrainAsync(
        string featureId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void Activate(string featureId)
    {
        _quiescingFeatures.Remove(featureId);
    }

    private sealed class TestContactCenterFeatureWorkLease : IContactCenterFeatureWorkLease
    {
        private TestContactCenterFeatureWorkManager _manager;

        public TestContactCenterFeatureWorkLease(TestContactCenterFeatureWorkManager manager)
        {
            _manager = manager;
            Interlocked.Increment(ref manager._activeLeaseCount);
        }

        public void Dispose()
        {
            var manager = Interlocked.Exchange(ref _manager, null);

            if (manager is not null)
            {
                Interlocked.Decrement(ref manager._activeLeaseCount);
            }
        }
    }
}

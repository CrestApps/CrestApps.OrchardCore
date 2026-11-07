using OrchardCore.Locking;

namespace CrestApps.OrchardCore.Locking;

/// <summary>
/// Holds the lockers that <see cref="DistributedLockExtensions.LockAllAsync"/> took, and releases them in reverse order.
/// </summary>
internal sealed class MultiKeyLocker : ILocker
{
    private List<ILocker> _lockers;

    public MultiKeyLocker(List<ILocker> lockers)
    {
        _lockers = lockers;
    }

    public ValueTask DisposeAsync()
    {
        var lockers = Interlocked.Exchange(ref _lockers, null);

        return new ValueTask(ReleaseAsync(lockers));
    }

    public void Dispose()
    {
        var lockers = Interlocked.Exchange(ref _lockers, null);

        if (lockers is null)
        {
            return;
        }

        for (var index = lockers.Count - 1; index >= 0; index--)
        {
            lockers[index]?.Dispose();
        }
    }

    internal static async Task ReleaseAsync(List<ILocker> lockers)
    {
        if (lockers is null)
        {
            return;
        }

        for (var index = lockers.Count - 1; index >= 0; index--)
        {
            if (lockers[index] is { } locker)
            {
                await locker.DisposeAsync();
            }
        }
    }
}

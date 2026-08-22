namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Serializes supervisor engagement start and stop operations that share the same deterministic engagement identity
/// (interaction plus supervisor) within a single node.
/// </summary>
/// <remarks>
/// Supervisor bridge, channel, and snoop ids are deterministic from the engagement key, so a concurrent duplicate
/// start would otherwise race the readiness signal (which completes an existing same-key waiter with <c>false</c>)
/// and compensate the shared, still-live engagement. A fixed set of stripes bounds the lock memory regardless of call
/// volume; a hash collision only adds rare, brief contention between two unrelated engagements. The stripes are
/// process-wide static so two shell generations serialize against the same deterministic ARI resources. They hold no
/// tenant data: CC-1 tenant isolation is still enforced because every ARI operation flows through the ownership gate.
/// </remarks>
internal static class AsteriskSupervisorEngagementLock
{
    private const int StripeCount = 64;

    private static readonly SemaphoreSlim[] _stripes = CreateStripes();

    /// <summary>
    /// Acquires the per-engagement stripe lock for the supplied engagement key, waiting until it is available.
    /// </summary>
    /// <param name="engagementKey">The stable engagement key that selects the stripe to serialize on.</param>
    /// <param name="cancellationToken">A token used to cancel the wait for the lock.</param>
    /// <returns>A disposable that releases the lock when disposed.</returns>
    public static async Task<IDisposable> AcquireAsync(string engagementKey, CancellationToken cancellationToken)
    {
        var semaphore = GetStripe(engagementKey);

        await semaphore.WaitAsync(cancellationToken);

        return new Releaser(semaphore);
    }

    private static SemaphoreSlim[] CreateStripes()
    {
        var stripes = new SemaphoreSlim[StripeCount];

        for (var i = 0; i < StripeCount; i++)
        {
            stripes[i] = new SemaphoreSlim(1, 1);
        }

        return stripes;
    }

    private static SemaphoreSlim GetStripe(string engagementKey)
    {
        var stripe = (uint)(engagementKey?.GetHashCode(StringComparison.Ordinal) ?? 0) % StripeCount;

        return _stripes[stripe];
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _released;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            _semaphore.Release();
        }
    }
}

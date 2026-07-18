using CrestApps.OrchardCore.Asterisk.Indexes;
using CrestApps.OrchardCore.Asterisk.Models;
using OrchardCore.Environment.Shell;
using YesSql;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// YesSql-backed implementation of <see cref="IAsteriskChannelTenantBindingStore"/>. Every operation runs in
/// its OWN isolated session created from the tenant <see cref="IStore"/>, so a write becomes durable
/// immediately — independent of the ambient request scope — and a caller-to-agent binding is visible to the
/// realtime listener scope the instant it is created, before the live ARI bridge is exposed. Because all
/// sessions are opened from the tenant store, operations are inherently isolated to the current tenant and
/// never observe or mutate another tenant's bindings. The connect flow and terminal-event teardown coordinate
/// through document-version optimistic concurrency (compare-and-set), not an external lock, so the durable
/// state transition itself is the single linearization authority.
/// </summary>
internal sealed class AsteriskChannelTenantBindingStore : IAsteriskChannelTenantBindingStore
{
    // A compare-and-set can only lose to a genuinely concurrent committed writer, and there are at most two
    // contenders for a binding (connect finalization and one terminal-event teardown). A small bounded retry
    // therefore resolves every real contention while never spinning: on exhaustion the caller falls back to its
    // safe path (finalize compensates, teardown defers to the reconciler).
    private const int ConcurrencyRetryLimit = 5;

    // Concurrent creates for the SAME channel are serialized in-process through a fixed set of stripes. YesSql has
    // no unique constraint on ChannelId, so on a single node this striped async gate is the linearization point that
    // makes an inbound channel's ownership claim exactly-once even when two overlapping same-tenant listener
    // generations (a shell-reload window) deliver the same StasisStart. A fixed stripe count bounds the lock memory
    // regardless of call volume; a hash collision only adds rare, brief contention between two unrelated channels.
    // The stripes are process-wide static because two shell generations resolve two distinct store instances yet must
    // serialize against the same underlying tenant database.
    private const int CreateLockStripeCount = 64;

    private static readonly SemaphoreSlim[] _createLocks = CreateCreateLocks();

    private readonly IStore _store;
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskChannelTenantBindingStore"/> class.
    /// </summary>
    /// <param name="store">The tenant YesSql store used to open isolated, immediately committed sessions.</param>
    /// <param name="shellSettings">The tenant shell settings used to scope the per-channel create serialization to this tenant.</param>
    public AsteriskChannelTenantBindingStore(
        IStore store,
        ShellSettings shellSettings)
    {
        _store = store;
        _shellSettings = shellSettings;
    }

    private static SemaphoreSlim[] CreateCreateLocks()
    {
        var locks = new SemaphoreSlim[CreateLockStripeCount];

        for (var i = 0; i < CreateLockStripeCount; i++)
        {
            locks[i] = new SemaphoreSlim(1, 1);
        }

        return locks;
    }

    private static SemaphoreSlim GetCreateLock(string tenantName, string channelId)
    {
        var stripe = (uint)HashCode.Combine(tenantName, channelId) % CreateLockStripeCount;

        return _createLocks[stripe];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<AsteriskChannelTenantBinding>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var session = _store.CreateSession();
        var bindings = await session
            .Query<AsteriskChannelTenantBinding, AsteriskChannelTenantBindingIndex>()
            .ListAsync(cancellationToken);

        return bindings is null ? [] : bindings.ToArray();
    }

    /// <inheritdoc/>
    public async Task<AsteriskChannelTenantBinding> FindByChannelIdAsync(string channelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        await using var session = _store.CreateSession();

        return await session
            .Query<AsteriskChannelTenantBinding, AsteriskChannelTenantBindingIndex>(index =>
                index.ChannelId == channelId)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<AsteriskChannelTenantBinding>> FindAllByPeerChannelIdAsync(string peerChannelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerChannelId);

        await using var session = _store.CreateSession();

        var bindings = await session
            .Query<AsteriskChannelTenantBinding, AsteriskChannelTenantBindingIndex>(index =>
                index.PeerChannelId == peerChannelId)
            .ListAsync();

        return bindings is null ? [] : bindings.ToArray();
    }

    /// <inheritdoc/>
    public async Task<bool> HasAnyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var session = _store.CreateSession();

        var binding = await session
            .Query<AsteriskChannelTenantBinding, AsteriskChannelTenantBindingIndex>()
            .FirstOrDefaultAsync(cancellationToken);

        return binding is not null;
    }

    /// <inheritdoc/>
    public async Task<bool> CreateAsync(AsteriskChannelTenantBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.ChannelId);

        // Serialize creates for this channel so an overlapping shell-reload window (two same-tenant listener
        // generations delivering the same StasisStart) claims it exactly once. The returned flag lets the caller
        // that loses the claim skip every inbound side effect it would otherwise repeat.
        var createLock = GetCreateLock(_shellSettings.Name, binding.ChannelId);
        await createLock.WaitAsync();

        try
        {
            await using var session = _store.CreateSession();

            var existing = await session
                .Query<AsteriskChannelTenantBinding, AsteriskChannelTenantBindingIndex>(index =>
                    index.ChannelId == binding.ChannelId)
                .FirstOrDefaultAsync();

            if (existing is not null)
            {
                return false;
            }

            session.Save(binding);
            await session.SaveChangesAsync();

            return true;
        }
        finally
        {
            createLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> MarkConnectedAsync(string channelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        for (var attempt = 0; attempt < ConcurrencyRetryLimit; attempt++)
        {
            await using var session = _store.CreateSession();

            var binding = await session
                .Query<AsteriskChannelTenantBinding, AsteriskChannelTenantBindingIndex>(index =>
                    index.ChannelId == channelId)
                .FirstOrDefaultAsync();

            // Only a still-pending agent leg may be promoted. A missing binding or any non-pending state means a
            // terminal event has already claimed it for teardown, so the connect flow must not report success.
            if (binding is null || binding.State != AsteriskChannelBindingState.Pending)
            {
                return false;
            }

            binding.State = AsteriskChannelBindingState.Connected;

            try
            {
                await session.SaveAsync(binding, checkConcurrency: true);
                await session.SaveChangesAsync();
            }
            catch (ConcurrencyException)
            {
                // A concurrent teardown committed to this binding after it was read. The session is now canceled
                // and cannot be reused; re-read in a fresh session, where the state check above will observe the
                // teardown and reject the promotion.
                continue;
            }

            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> TryPromoteOfferingAsync(string channelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        for (var attempt = 0; attempt < ConcurrencyRetryLimit; attempt++)
        {
            await using var session = _store.CreateSession();

            var binding = await session
                .Query<AsteriskChannelTenantBinding, AsteriskChannelTenantBindingIndex>(index =>
                    index.ChannelId == channelId)
                .FirstOrDefaultAsync();

            // Only a still-offering caller leg may be promoted. A missing binding or any non-offering state means a
            // terminal event has already claimed it for teardown, so the offer flow must not report the caller as
            // routed-and-live.
            if (binding is null || binding.State != AsteriskChannelBindingState.Offering)
            {
                return false;
            }

            binding.State = AsteriskChannelBindingState.Connected;

            try
            {
                await session.SaveAsync(binding, checkConcurrency: true);
                await session.SaveChangesAsync();
            }
            catch (ConcurrencyException)
            {
                // A concurrent teardown committed to this binding after it was read. The session is now canceled
                // and cannot be reused; re-read in a fresh session, where the state check above will observe the
                // teardown and reject the promotion.
                continue;
            }

            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> MarkCallerDetachedAsync(string channelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        for (var attempt = 0; attempt < ConcurrencyRetryLimit; attempt++)
        {
            await using var session = _store.CreateSession();

            var binding = await session
                .Query<AsteriskChannelTenantBinding, AsteriskChannelTenantBindingIndex>(index =>
                    index.ChannelId == channelId)
                .FirstOrDefaultAsync();

            // Only a still-pending agent leg records the caller-detached marker. A missing or non-pending binding
            // means a terminal event already claimed it for teardown, which owns the caller's disposition, so the
            // marker is unnecessary and the connect flow will observe the lost race when it finalizes.
            if (binding is null || binding.State != AsteriskChannelBindingState.Pending)
            {
                return false;
            }

            if (binding.CallerDetached)
            {
                return true;
            }

            binding.CallerDetached = true;

            try
            {
                await session.SaveAsync(binding, checkConcurrency: true);
                await session.SaveChangesAsync();
            }
            catch (ConcurrencyException)
            {
                // A concurrent teardown committed to this binding after it was read. The session is canceled;
                // re-read in a fresh session, where the state check above will observe the teardown and stop.
                continue;
            }

            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<AsteriskChannelTeardownClaim> TryBeginTeardownAsync(string channelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        for (var attempt = 0; attempt < ConcurrencyRetryLimit; attempt++)
        {
            await using var session = _store.CreateSession();

            var binding = await session
                .Query<AsteriskChannelTenantBinding, AsteriskChannelTenantBindingIndex>(index =>
                    index.ChannelId == channelId)
                .FirstOrDefaultAsync();

            // A missing binding means the call is already fully cleaned up; an already-terminating binding means
            // another terminal event owns the teardown. In both cases there is nothing to claim.
            if (binding is null || binding.State == AsteriskChannelBindingState.Terminating)
            {
                return null;
            }

            var previousState = binding.State;
            binding.State = AsteriskChannelBindingState.Terminating;
            binding.PreTeardownState = previousState;

            try
            {
                await session.SaveAsync(binding, checkConcurrency: true);
                await session.SaveChangesAsync();
            }
            catch (ConcurrencyException)
            {
                // The connect flow (or another writer) committed to this binding after it was read. The session
                // is canceled; re-read in a fresh session and re-evaluate whether there is still a claim to make.
                continue;
            }

            return new AsteriskChannelTeardownClaim
            {
                Binding = binding,
                PreviousState = previousState,
            };
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task RemoveByChannelIdAsync(string channelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        await using var session = _store.CreateSession();

        var binding = await session
            .Query<AsteriskChannelTenantBinding, AsteriskChannelTenantBindingIndex>(index =>
                index.ChannelId == channelId)
            .FirstOrDefaultAsync();

        if (binding is null)
        {
            return;
        }

        session.Delete(binding);
        await session.SaveChangesAsync();
    }
}

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
    public async Task<bool> SwapConnectedOwnerAsync(string newAgentChannelId, string previousAgentChannelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newAgentChannelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(previousAgentChannelId);

        for (var attempt = 0; attempt < ConcurrencyRetryLimit; attempt++)
        {
            await using var session = _store.CreateSession();

            var newBinding = await session
                .Query<AsteriskChannelTenantBinding, AsteriskChannelTenantBindingIndex>(index =>
                    index.ChannelId == newAgentChannelId)
                .FirstOrDefaultAsync();

            // Only a still-joining destination leg may be promoted. A missing binding or any non-joining state means
            // a terminal event has already claimed the destination leg for teardown, so the transfer finalization
            // must not retire the previous agent leg — the caller safely stays with the previous agent.
            if (newBinding is null || newBinding.State != AsteriskChannelBindingState.Joining)
            {
                return false;
            }

            var previousBinding = await session
                .Query<AsteriskChannelTenantBinding, AsteriskChannelTenantBindingIndex>(index =>
                    index.ChannelId == previousAgentChannelId)
                .FirstOrDefaultAsync();

            // The destination may be promoted ONLY when the previous agent leg is still the live Connected owner of
            // the conversation. A missing or non-Connected previous binding means ownership was already swapped away
            // by another transfer (or the previous agent's own terminal event claimed it), so promoting this
            // destination would create a SECOND Connected owner of the shared canonical bridge — the exact
            // double-teardown drop this swap exists to prevent. Reject instead: the destination stays a non-owning
            // Joining leg the reconciler reclaims, and the customer keeps the agent that won ownership.
            if (previousBinding is null || previousBinding.State != AsteriskChannelBindingState.Connected)
            {
                return false;
            }

            newBinding.State = AsteriskChannelBindingState.Connected;

            // Retire the previous owner by transitioning it to a NON-OWNING terminating state whose Joining
            // pre-teardown disposition tells the teardown planner and reconciler to hang up ONLY the previous
            // channel — never the shared canonical bridge or the caller. This is deliberately a concurrency-checked
            // state transition, not a raw delete: a YesSql delete is an id-only operation with no version fence, so
            // two transfers of the same call to DIFFERENT destinations could both read the previous owner as
            // Connected, both promote their own destination (their concurrency checks target distinct destination
            // bindings and never conflict), and both delete the previous row — the second deleting zero rows but
            // still committing — leaving TWO Connected owners of the one bridge. Making the previous-owner transition
            // a version-checked write turns it into the single linearization point: only the first swap can move the
            // previous binding out of Connected, and the loser's version-checked write fails, re-reads, and rejects.
            previousBinding.State = AsteriskChannelBindingState.Terminating;
            previousBinding.PreTeardownState = AsteriskChannelBindingState.Joining;

            try
            {
                // Promote the destination leg and retire the previous leg in ONE transaction. BOTH writes are
                // version-checked: the destination promotion fences a racing teardown that claimed the destination,
                // and the previous-owner retirement fences a second concurrent transfer racing to promote a different
                // destination. Committing both atomically guarantees no terminal event can ever see two Connected
                // owners of the canonical bridge or a moment with none.
                await session.SaveAsync(newBinding, checkConcurrency: true);
                await session.SaveAsync(previousBinding, checkConcurrency: true);
                await session.SaveChangesAsync();
            }
            catch (ConcurrencyException)
            {
                // A concurrent teardown committed to the destination binding, or a concurrent transfer already retired
                // this previous owner, after either was read. The session is canceled; re-read in a fresh session,
                // where the state checks above will observe the change and reject the swap so the caller safely stays
                // with whichever agent won ownership.
                continue;
            }

            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> TryPromoteJoiningToParticipatingAsync(string channelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        for (var attempt = 0; attempt < ConcurrencyRetryLimit; attempt++)
        {
            await using var session = _store.CreateSession();

            var binding = await session
                .Query<AsteriskChannelTenantBinding, AsteriskChannelTenantBindingIndex>(index =>
                    index.ChannelId == channelId)
                .FirstOrDefaultAsync();

            // Only a still-joining participant leg may be promoted. A missing binding or any non-joining state means
            // a terminal event has already claimed the participant leg for teardown, so the conference flow must not
            // treat it as a live member.
            if (binding is null || binding.State != AsteriskChannelBindingState.Joining)
            {
                return false;
            }

            binding.State = AsteriskChannelBindingState.Participating;

            try
            {
                await session.SaveAsync(binding, checkConcurrency: true);
                await session.SaveChangesAsync();
            }
            catch (ConcurrencyException)
            {
                // A terminal event (or another writer) committed to this binding after it was read. The session is
                // canceled; re-read in a fresh session and re-evaluate whether the leg is still joining.
                continue;
            }

            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> TryHandOffBridgeOwnershipAsync(string bridgeId, string departingOwnerChannelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bridgeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(departingOwnerChannelId);

        for (var attempt = 0; attempt < ConcurrencyRetryLimit; attempt++)
        {
            await using var session = _store.CreateSession();

            // Scan for the departing owner and a remaining participating leg on the shared bridge in memory rather
            // than through a dedicated index: at most a handful of legs are ever bound to one live conversation at a
            // time, so the volume is tiny and a full scan avoids an index/migration change for a rarely hit
            // ownership-handoff path.
            var bindings = await session
                .Query<AsteriskChannelTenantBinding, AsteriskChannelTenantBindingIndex>()
                .ListAsync();

            var owner = bindings?
                .FirstOrDefault(binding =>
                    string.Equals(binding.ChannelId, departingOwnerChannelId, StringComparison.Ordinal));

            // A concurrent hand-off (a racing teardown or reconciler sweep on the same departing owner) already
            // retired this owner to a non-owning disposition, or already removed its record entirely. In both cases
            // the bridge is now owned by the promoted participant, so this caller must NOT tear it down.
            if (owner is null ||
                (owner.State == AsteriskChannelBindingState.Terminating &&
                    owner.PreTeardownState == AsteriskChannelBindingState.Joining))
            {
                return true;
            }

            var participant = bindings
                .FirstOrDefault(binding =>
                    binding.State == AsteriskChannelBindingState.Participating &&
                    string.Equals(binding.BridgeId, bridgeId, StringComparison.Ordinal) &&
                    !string.Equals(binding.ChannelId, departingOwnerChannelId, StringComparison.Ordinal));

            // No remaining participant means the departing owner is the last agent, so the caller and bridge may be
            // released by the departing owner's own teardown path.
            if (participant is null)
            {
                return false;
            }

            participant.State = AsteriskChannelBindingState.Connected;

            // Retire the departing owner to a NON-OWNING terminating disposition in the SAME transaction as the
            // promotion. A YesSql delete is an id-only operation with no version fence, and the owner's record removal
            // happens in a later, separate session; if that removal never lands (a transient ARI failure or crash
            // between committing this promotion and removing the record), a subsequent sweep would otherwise re-read
            // the owner as a live Connected owner and either destroy the now-live promoted bridge or promote a SECOND
            // owner. Flipping the owner's pre-teardown disposition to Joining here — version-checked, atomic with the
            // promotion — makes this the single linearization point: any later processing of the owner sees a
            // non-owning leg that only hangs up its own channel, and a concurrent hand-off loses the version race,
            // re-reads, and observes the already-handed-off owner.
            owner.PreTeardownState = AsteriskChannelBindingState.Joining;

            try
            {
                await session.SaveAsync(participant, checkConcurrency: true);
                await session.SaveAsync(owner, checkConcurrency: true);
                await session.SaveChangesAsync();
            }
            catch (ConcurrencyException)
            {
                // Another writer committed to the selected participant or the departing owner after either was read
                // (a concurrent owner departure promoted it, its own terminal event claimed it for teardown, or a
                // racing hand-off retired the owner). Re-read in a fresh session and re-evaluate.
                continue;
            }

            return true;
        }

        // The records could not be resolved deterministically within the retry budget, which means another writer is
        // actively mutating them — most likely a concurrent hand-off completing. The safe direction is to NOT destroy
        // a possibly-live promoted bridge, so report the bridge as retained.
        return true;
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

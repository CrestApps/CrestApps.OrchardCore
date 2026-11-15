using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Commerce.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Ledgers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Commerce.Core.Services;

/// <summary>
/// Keeps licence pools and their seats. The seats left are the balance of the seat ledger: the purchase posts the seats
/// bought, each seat given takes one, and each seat taken back returns one, every post once by its key. Every change of
/// a pool's seats runs under one lock per pool, on fresh reads, and is committed before the lock is let go; enrolling
/// and telling observers happen after. Giving a seat enrolls the person with the seat as the grant, so taking the seat
/// back ends exactly that enrollment, and a seat whose enrollment fails is given back.
/// </summary>
public sealed class DefaultLicensePoolService : ILicensePoolService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private readonly LicensePoolStore _pools;
    private readonly LicenseSeatStore _seats;
    private readonly IQuantityLedger<SeatLedger> _ledger;
    private readonly IContentManager _contentManager;
    private readonly IDistributedLock _distributedLock;
    private readonly IServiceProvider _services;
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultLicensePoolService(
        LicensePoolStore pools,
        LicenseSeatStore seats,
        IQuantityLedger<SeatLedger> ledger,
        IContentManager contentManager,
        IDistributedLock distributedLock,
        IServiceProvider services,
        ISession session,
        IClock clock,
        IStringLocalizer<DefaultLicensePoolService> stringLocalizer)
    {
        _pools = pools;
        _seats = seats;
        _ledger = ledger;
        _contentManager = contentManager;
        _distributedLock = distributedLock;
        _services = services;
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<LicensePool> CreateAsync(LicensePool pool, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pool);

        // One pool per order line, however often and however concurrently the order-paid event arrives: the check
        // and the create run under a lock of the line, and the purchase is posted under a key of the line.
        var lineKey = string.IsNullOrEmpty(pool.OrderId) ? null : $"{pool.OrderId}:{pool.LineId}";
        LicensePool created;

        if (lineKey is null)
        {
            created = await CreateCoreAsync(pool, $"purchase:{pool.ItemId}", cancellationToken);
        }
        else
        {
            var (locker, locked) = await _distributedLock.TryAcquireLockAsync($"Learning:LicensePool:Line:{lineKey}", _lockTimeout, _lockExpiration);

            if (!locked)
            {
                throw new InvalidOperationException("Another request is creating the seats of this order line.");
            }

            await using (locker)
            {
                if (await _pools.FindFreshByLineAsync(pool.OrderId, pool.LineId, cancellationToken) is { } existing)
                {
                    return existing;
                }

                created = await CreateCoreAsync(pool, $"purchase:{lineKey}", cancellationToken);
            }
        }

        foreach (var observer in _services.GetServices<ILicensePoolObserver>())
        {
            await observer.SeatsPurchasedAsync(created, cancellationToken);
        }

        return created;
    }

    /// <inheritdoc/>
    public async Task<LicensePool> FindAsync(string poolId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(poolId) ? null : await _pools.FindByIdAsync(poolId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<LicensePool>> ListForAccountAsync(string accountId, CancellationToken cancellationToken = default)
        => _pools.ListByAccountAsync(accountId, cancellationToken);

    /// <inheritdoc/>
    public async Task<int> GetRemainingAsync(LicensePool pool, CancellationToken cancellationToken = default)
        => pool is null || pool.State != LicensePoolState.Active ? 0 : (int)await _ledger.GetBalanceAsync(pool.AccountId, pool.ItemId, DateOnly.FromDateTime(_clock.UtcNow), cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<LicenseSeat>> ListSeatsAsync(string poolId, CancellationToken cancellationToken = default)
        => _seats.ListByPoolAsync(poolId, cancellationToken);

    /// <inheritdoc/>
    public async Task<LicenseSeatResult> GiveAsync(string poolId, string userId, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(poolId))
        {
            return new LicenseSeatResult(null, S["The seats were not found or were taken back."]);
        }

        var (locker, locked) = await LockPoolAsync(poolId);

        if (!locked)
        {
            return new LicenseSeatResult(null, S["Someone else is changing these seats. Try again in a moment."]);
        }

        LicensePool pool;
        LicenseSeat seat;

        await using (locker)
        {
            pool = await _pools.FindFreshByIdAsync(poolId, cancellationToken);

            var error = await CheckPoolAsync(pool, cancellationToken);

            if (error is not null)
            {
                return new LicenseSeatResult(null, error);
            }

            var member = await FindMemberAsync(pool.AccountId, userId);

            if (member is null)
            {
                return new LicenseSeatResult(null, S["Only a member of the account can get one of its seats."]);
            }

            if (await HoldsSeatAsync(pool.ItemId, userId, exceptSeatId: null, cancellationToken))
            {
                return new LicenseSeatResult(null, S["{0} already has a seat in this course.", member.UserName ?? userId]);
            }

            seat = new LicenseSeat
            {
                PoolId = pool.ItemId,
                AccountId = pool.AccountId,
                UserId = userId,
                Email = member.Email,
                State = LicenseSeatState.Assigned,
                GivenBy = actor,
                GivenUtc = _clock.UtcNow,
                AssignedUtc = _clock.UtcNow,
            };

            await TakeSeatAsync(pool, seat, cancellationToken);
        }

        var enrolled = await EnrollAsync(pool, seat, actor, cancellationToken);

        return enrolled is null ? new LicenseSeatResult(seat) : new LicenseSeatResult(null, enrolled);
    }

    /// <inheritdoc/>
    public async Task<LicenseSeatResult> InviteAsync(string poolId, string email, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(poolId))
        {
            return new LicenseSeatResult(null, S["The seats were not found or were taken back."]);
        }

        LicensePool pool;
        LicenseSeat seat;
        string memberUserId = null;

        var (locker, locked) = await LockPoolAsync(poolId);

        if (!locked)
        {
            return new LicenseSeatResult(null, S["Someone else is changing these seats. Try again in a moment."]);
        }

        await using (locker)
        {
            pool = await _pools.FindFreshByIdAsync(poolId, cancellationToken);

            var error = await CheckPoolAsync(pool, cancellationToken);

            if (error is not null)
            {
                return new LicenseSeatResult(null, error);
            }

            // A member of the account gets the seat at once; only people outside it are invited.
            var account = await _contentManager.GetAsync(pool.AccountId, VersionOptions.Published);
            var member = account is not null && account.TryGet<AccountMembersPart>(out var members)
                ? members.Members?.FirstOrDefault(candidate => string.Equals(candidate.Email, email?.Trim(), StringComparison.OrdinalIgnoreCase))
                : null;

            if (member is not null)
            {
                memberUserId = member.UserId;
                seat = null;
            }
            else
            {
                seat = new LicenseSeat
                {
                    PoolId = pool.ItemId,
                    AccountId = pool.AccountId,
                    Email = email?.Trim(),
                    State = LicenseSeatState.Invited,
                    GivenBy = actor,
                    GivenUtc = _clock.UtcNow,
                };

                // The seat is held before the invitation is sent, so two invitations can't share the last seat.
                await TakeSeatAsync(pool, seat, cancellationToken);
            }
        }

        if (memberUserId is not null)
        {
            return await GiveAsync(poolId, memberUserId, actor, cancellationToken);
        }

        var invitation = await _services.GetRequiredService<IAccountInvitationService>().InviteAsync(new AccountInvitationRequest
        {
            AccountId = pool.AccountId,
            Email = email,
            Role = AccountMemberRole.Member,
            InvitedBy = actor,
            Source = LearningCommerceConstants.SeatSource,
            SourceId = seat.ItemId,
            Message = S["A seat in {0} is waiting for you.", pool.Title ?? string.Empty],
        }, cancellationToken);

        if (!invitation.Succeeded)
        {
            await ReleaseHeldSeatAsync(seat.ItemId, LicenseSeatState.Invited, actor, cancellationToken);

            return new LicenseSeatResult(null, invitation.Error);
        }

        var (seatLocker, seatLocked) = await LockPoolAsync(poolId);

        if (seatLocked)
        {
            await using (seatLocker)
            {
                seat = await _seats.FindFreshByIdAsync(seat.ItemId, cancellationToken);
                seat.InvitationId = invitation.Invitation.ItemId;
                await _seats.UpdateAsync(seat, cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);
            }
        }

        return new LicenseSeatResult(seat);
    }

    /// <summary>
    /// Gives the held seat to the person who accepted its invitation. A person who already holds a seat of the pool
    /// keeps that one, and the held seat is given back.
    /// </summary>
    internal async Task AssignInvitedAsync(AccountInvitation invitation, CancellationToken cancellationToken = default)
    {
        var found = await FindSeatOfInvitationAsync(invitation, cancellationToken);

        if (found is null)
        {
            return;
        }

        var (locker, locked) = await LockPoolAsync(found.PoolId);

        if (!locked)
        {
            throw new InvalidOperationException("Someone else is changing these seats.");
        }

        LicensePool pool;
        LicenseSeat seat;

        await using (locker)
        {
            seat = await _seats.FindFreshByIdAsync(found.ItemId, cancellationToken);
            pool = await _pools.FindFreshByIdAsync(found.PoolId, cancellationToken);

            if (seat is null || pool is null || seat.State != LicenseSeatState.Invited || pool.State != LicensePoolState.Active)
            {
                return;
            }

            if (await HoldsSeatAsync(pool.ItemId, invitation.AcceptedUserId, seat.ItemId, cancellationToken))
            {
                await ReleaseLockedAsync(pool, seat, cancellationToken);

                return;
            }

            seat.UserId = invitation.AcceptedUserId;
            seat.State = LicenseSeatState.Assigned;
            seat.AssignedUtc = _clock.UtcNow;
            await _seats.UpdateAsync(seat, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }

        await EnrollAsync(pool, seat, seat.GivenBy, cancellationToken);
    }

    /// <summary>
    /// Gives back the seat an invitation held when the invitation was taken back, replaced or expired, once.
    /// </summary>
    internal async Task ReleaseForInvitationAsync(AccountInvitation invitation, CancellationToken cancellationToken = default)
    {
        var found = await FindSeatOfInvitationAsync(invitation, cancellationToken);

        if (found is not null)
        {
            await ReleaseHeldSeatAsync(found.ItemId, LicenseSeatState.Invited, "system", cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<LicenseSeatResult> RemoveAsync(string seatId, string actor, CancellationToken cancellationToken = default)
    {
        var found = string.IsNullOrEmpty(seatId) ? null : await _seats.FindByIdAsync(seatId, cancellationToken);

        if (found is null)
        {
            return new LicenseSeatResult(null, S["The seat was not found or was already taken back."]);
        }

        var (locker, locked) = await LockPoolAsync(found.PoolId);

        if (!locked)
        {
            return new LicenseSeatResult(null, S["Someone else is changing these seats. Try again in a moment."]);
        }

        LicenseSeat seat;
        LicenseSeatState before;

        await using (locker)
        {
            seat = await _seats.FindFreshByIdAsync(seatId, cancellationToken);
            var pool = await _pools.FindFreshByIdAsync(found.PoolId, cancellationToken);

            if (seat is null || pool is null || seat.State == LicenseSeatState.Removed)
            {
                return new LicenseSeatResult(null, S["The seat was not found or was already taken back."]);
            }

            before = seat.State;
            await ReleaseLockedAsync(pool, seat, cancellationToken);
        }

        if (before == LicenseSeatState.Assigned)
        {
            await _services.GetRequiredService<IEnrollmentService>().RevokeGrantAsync(LearningCommerceConstants.SeatSource, seat.ItemId, actor, S["The seat was taken back."], cancellationToken);
        }
        else if (!string.IsNullOrEmpty(seat.InvitationId))
        {
            await _services.GetRequiredService<IAccountInvitationService>().RevokeAsync(seat.InvitationId, actor, cancellationToken);
        }

        return new LicenseSeatResult(seat);
    }

    /// <inheritdoc/>
    public async Task<LicensePool> RevokeAsync(string poolId, string reason, CancellationToken cancellationToken = default)
    {
        var (locker, locked) = await LockPoolAsync(poolId);

        if (!locked)
        {
            throw new InvalidOperationException("Someone else is changing these seats.");
        }

        LicensePool pool;
        List<LicenseSeat> released = [];

        await using (locker)
        {
            pool = await _pools.FindFreshByIdAsync(poolId, cancellationToken);

            if (pool is null || pool.State == LicensePoolState.Revoked)
            {
                return pool;
            }

            foreach (var listed in await _seats.ListByPoolAsync(pool.ItemId, cancellationToken))
            {
                var seat = await _seats.FindFreshByIdAsync(listed.ItemId, cancellationToken);

                if (seat is null || seat.State == LicenseSeatState.Removed)
                {
                    continue;
                }

                released.Add(new LicenseSeat { ItemId = seat.ItemId, State = seat.State, InvitationId = seat.InvitationId });
                seat.State = LicenseSeatState.Removed;
                seat.RemovedUtc = _clock.UtcNow;
                await _seats.UpdateAsync(seat, cancellationToken);
            }

            pool.State = LicensePoolState.Revoked;
            await _pools.UpdateAsync(pool, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }

        foreach (var seat in released)
        {
            if (seat.State == LicenseSeatState.Assigned)
            {
                await _services.GetRequiredService<IEnrollmentService>().RevokeGrantAsync(LearningCommerceConstants.SeatSource, seat.ItemId, "system", reason, cancellationToken);
            }
            else if (!string.IsNullOrEmpty(seat.InvitationId))
            {
                await _services.GetRequiredService<IAccountInvitationService>().RevokeAsync(seat.InvitationId, "system", cancellationToken);
            }
        }

        return await _pools.FindFreshByIdAsync(poolId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<LicenseSeat>> ListSeatsForPeopleAsync(IReadOnlyCollection<string> userIds, IReadOnlyCollection<string> emails, CancellationToken cancellationToken = default)
        => _seats.ListForPeopleAsync(
            (userIds ?? []).Where(id => !string.IsNullOrEmpty(id)).ToArray(),
            (emails ?? []).Where(email => !string.IsNullOrWhiteSpace(email)).Select(email => email.Trim().ToUpperInvariant()).ToArray(),
            cancellationToken);

    private async Task<LicensePool> CreateCoreAsync(LicensePool pool, string purchaseKey, CancellationToken cancellationToken)
    {
        pool.State = LicensePoolState.Active;
        pool.CreatedUtc = _clock.UtcNow;
        await _pools.CreateAsync(pool, cancellationToken);
        await _ledger.PostAsync(new QuantityLedgerEntry
        {
            AccountId = pool.AccountId,
            Bucket = pool.ItemId,
            Kind = "Purchase",
            Amount = pool.Seats,
            EffectiveDate = DateOnly.FromDateTime(_clock.UtcNow),
            SourceRef = pool.OrderId,
            IdempotencyKey = purchaseKey,
            Note = pool.Title,
        }, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return pool;
    }

    private Task<(ILocker Locker, bool Locked)> LockPoolAsync(string poolId)
        => _distributedLock.TryAcquireLockAsync($"Learning:LicensePool:{poolId}", _lockTimeout, _lockExpiration);

    private async Task<AccountMember> FindMemberAsync(string accountId, string userId)
    {
        var account = await _contentManager.GetAsync(accountId, VersionOptions.Published);

        return account is not null && account.TryGet<AccountMembersPart>(out var members) ? members.FindMember(userId) : null;
    }

    private async Task<bool> HoldsSeatAsync(string poolId, string userId, string exceptSeatId, CancellationToken cancellationToken)
        => !string.IsNullOrEmpty(userId)
            && (await _seats.ListByPoolAsync(poolId, cancellationToken)).Any(seat => seat.UserId == userId && seat.State == LicenseSeatState.Assigned && seat.ItemId != exceptSeatId);

    private async Task<LicenseSeat> FindSeatOfInvitationAsync(AccountInvitation invitation, CancellationToken cancellationToken)
    {
        if (invitation?.Source != LearningCommerceConstants.SeatSource)
        {
            return null;
        }

        return await _seats.FindByInvitationAsync(invitation.ItemId, cancellationToken)
            ?? (string.IsNullOrEmpty(invitation.SourceId) ? null : await _seats.FindByIdAsync(invitation.SourceId, cancellationToken));
    }

    private async Task<string> CheckPoolAsync(LicensePool pool, CancellationToken cancellationToken)
    {
        if (pool is null || pool.State != LicensePoolState.Active)
        {
            return S["The seats were not found or were taken back."];
        }

        return await GetRemainingAsync(pool, cancellationToken) <= 0 ? S["Every seat of this course is taken."] : null;
    }

    private async Task TakeSeatAsync(LicensePool pool, LicenseSeat seat, CancellationToken cancellationToken)
    {
        await _seats.CreateAsync(seat, cancellationToken);
        await _ledger.PostAsync(new QuantityLedgerEntry
        {
            AccountId = pool.AccountId,
            Bucket = pool.ItemId,
            Kind = "Seat",
            Amount = -1,
            EffectiveDate = DateOnly.FromDateTime(_clock.UtcNow),
            SourceRef = seat.ItemId,
            IdempotencyKey = $"seat:{seat.ItemId}",
            Actor = seat.GivenBy,
        }, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }

    // Marks a seat given back and returns it to the pool, once; the caller holds the pool lock.
    private async Task ReleaseLockedAsync(LicensePool pool, LicenseSeat seat, CancellationToken cancellationToken)
    {
        seat.State = LicenseSeatState.Removed;
        seat.RemovedUtc = _clock.UtcNow;
        await _seats.UpdateAsync(seat, cancellationToken);
        await _ledger.PostAsync(new QuantityLedgerEntry
        {
            AccountId = pool.AccountId,
            Bucket = pool.ItemId,
            Kind = "Release",
            Amount = 1,
            EffectiveDate = DateOnly.FromDateTime(_clock.UtcNow),
            SourceRef = seat.ItemId,
            IdempotencyKey = $"release:{seat.ItemId}",
            Actor = seat.GivenBy,
        }, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }

    // Gives back a seat still in the state it was taken in, under the pool lock.
    private async Task ReleaseHeldSeatAsync(string seatId, LicenseSeatState expected, string actor, CancellationToken cancellationToken)
    {
        var found = await _seats.FindByIdAsync(seatId, cancellationToken);

        if (found is null)
        {
            return;
        }

        var (locker, locked) = await LockPoolAsync(found.PoolId);

        if (!locked)
        {
            throw new InvalidOperationException("Someone else is changing these seats.");
        }

        await using (locker)
        {
            var seat = await _seats.FindFreshByIdAsync(seatId, cancellationToken);
            var pool = await _pools.FindFreshByIdAsync(found.PoolId, cancellationToken);

            if (seat is null || pool is null || seat.State != expected)
            {
                return;
            }

            await ReleaseLockedAsync(pool, seat, cancellationToken);
        }
    }

    // Enrolls the holder of a seat; a failed enrollment gives the seat back. Returns the reason it failed, or null.
    private async Task<string> EnrollAsync(LicensePool pool, LicenseSeat seat, string actor, CancellationToken cancellationToken)
    {
        var result = await _services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = seat.UserId,
            CourseId = pool.CourseId,
            Source = LearningCommerceConstants.SeatSource,
            SourceId = seat.ItemId,
            AccessEndsUtc = pool.AccessDays is > 0 ? _clock.UtcNow.AddDays(pool.AccessDays.Value) : null,
            Actor = actor,
        }, cancellationToken);

        if (!result.Succeeded)
        {
            await ReleaseHeldSeatAsync(seat.ItemId, LicenseSeatState.Assigned, actor, cancellationToken);

            return S["The person could not be enrolled ({0}), so the seat was given back.", result.ErrorCode ?? string.Empty];
        }

        var observers = _services.GetServices<ILicensePoolObserver>().ToArray();

        foreach (var observer in observers)
        {
            await observer.SeatGivenAsync(pool, seat, cancellationToken);
        }

        var remaining = await GetRemainingAsync(await _pools.FindFreshByIdAsync(pool.ItemId, cancellationToken), cancellationToken);

        if (pool.Seats > 0 && remaining * 10 <= pool.Seats)
        {
            foreach (var observer in observers)
            {
                await observer.RunningLowAsync(pool, remaining, cancellationToken);
            }
        }

        return null;
    }
}

/// <summary>
/// Gives a held seat to the person who accepts its account invitation, and gives the seat back to the pool when the
/// invitation is taken back, replaced or expires.
/// </summary>
internal sealed class LicenseSeatInvitationObserver : IAccountInvitationObserver
{
    private readonly IServiceProvider _services;

    public LicenseSeatInvitationObserver(IServiceProvider services)
    {
        _services = services;
    }

    public Task AcceptedAsync(AccountInvitation invitation, CancellationToken cancellationToken = default)
        => invitation.Source == LearningCommerceConstants.SeatSource
            ? Pools.AssignInvitedAsync(invitation, cancellationToken)
            : Task.CompletedTask;

    public Task RevokedAsync(AccountInvitation invitation, CancellationToken cancellationToken = default)
        => invitation.Source == LearningCommerceConstants.SeatSource
            ? Pools.ReleaseForInvitationAsync(invitation, cancellationToken)
            : Task.CompletedTask;

    public Task ExpiredAsync(AccountInvitation invitation, CancellationToken cancellationToken = default)
        => RevokedAsync(invitation, cancellationToken);

    private DefaultLicensePoolService Pools => (DefaultLicensePoolService)_services.GetRequiredService<ILicensePoolService>();
}

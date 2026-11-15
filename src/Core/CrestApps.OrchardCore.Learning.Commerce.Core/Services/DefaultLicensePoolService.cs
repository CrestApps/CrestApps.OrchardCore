using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Commerce.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Ledgers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Commerce.Core.Services;

/// <summary>
/// Keeps licence pools and their seats. The seats left are the balance of the seat ledger: the purchase posts the seats
/// bought, each seat given takes one, and each seat taken back returns one, every post once by its key. Giving a seat
/// enrolls the person with the seat as the grant, so taking the seat back ends exactly that enrollment.
/// </summary>
public sealed class DefaultLicensePoolService : ILicensePoolService
{
    private readonly LicensePoolStore _pools;
    private readonly LicenseSeatStore _seats;
    private readonly IQuantityLedger<SeatLedger> _ledger;
    private readonly IContentManager _contentManager;
    private readonly IServiceProvider _services;
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultLicensePoolService(
        LicensePoolStore pools,
        LicenseSeatStore seats,
        IQuantityLedger<SeatLedger> ledger,
        IContentManager contentManager,
        IServiceProvider services,
        ISession session,
        IClock clock,
        IStringLocalizer<DefaultLicensePoolService> stringLocalizer)
    {
        _pools = pools;
        _seats = seats;
        _ledger = ledger;
        _contentManager = contentManager;
        _services = services;
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<LicensePool> CreateAsync(LicensePool pool, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pool);

        if (!string.IsNullOrEmpty(pool.OrderId) && await _pools.FindByLineAsync(pool.OrderId, pool.LineId, cancellationToken) is { } existing)
        {
            return existing;
        }

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
            IdempotencyKey = $"purchase:{pool.ItemId}",
            Note = pool.Title,
        }, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        foreach (var observer in _services.GetServices<ILicensePoolObserver>())
        {
            await observer.SeatsPurchasedAsync(pool, cancellationToken);
        }

        return pool;
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
        var pool = await FindAsync(poolId, cancellationToken);
        var error = await CheckPoolAsync(pool, cancellationToken);

        if (error is not null)
        {
            return new LicenseSeatResult(null, error);
        }

        var account = await _contentManager.GetAsync(pool.AccountId, VersionOptions.Latest);
        var member = account is not null && account.TryGet<AccountMembersPart>(out var members) ? members.FindMember(userId) : null;

        if (member is null)
        {
            return new LicenseSeatResult(null, S["Only a member of the account can get one of its seats."]);
        }

        if ((await _seats.ListByPoolAsync(pool.ItemId, cancellationToken)).Any(seat => seat.UserId == userId && seat.State == LicenseSeatState.Assigned))
        {
            return new LicenseSeatResult(null, S["{0} already has a seat in this course.", member.UserName ?? userId]);
        }

        var seat = new LicenseSeat
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
        await EnrollAsync(pool, seat, actor, cancellationToken);

        return new LicenseSeatResult(seat);
    }

    /// <inheritdoc/>
    public async Task<LicenseSeatResult> InviteAsync(string poolId, string email, string actor, CancellationToken cancellationToken = default)
    {
        var pool = await FindAsync(poolId, cancellationToken);
        var error = await CheckPoolAsync(pool, cancellationToken);

        if (error is not null)
        {
            return new LicenseSeatResult(null, error);
        }

        // A member of the account gets the seat at once; only people outside it are invited.
        var account = await _contentManager.GetAsync(pool.AccountId, VersionOptions.Latest);
        var member = account is not null && account.TryGet<AccountMembersPart>(out var members)
            ? members.Members?.FirstOrDefault(candidate => string.Equals(candidate.Email, email?.Trim(), StringComparison.OrdinalIgnoreCase))
            : null;

        if (member is not null)
        {
            return await GiveAsync(poolId, member.UserId, actor, cancellationToken);
        }

        var seat = new LicenseSeat
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

        seat = await _seats.FindByIdAsync(seat.ItemId, cancellationToken);

        if (!invitation.Succeeded)
        {
            await ReleaseSeatAsync(pool, seat, actor, cancellationToken);

            return new LicenseSeatResult(null, invitation.Error);
        }

        seat.InvitationId = invitation.Invitation.ItemId;
        await _seats.UpdateAsync(seat, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return new LicenseSeatResult(seat);
    }

    /// <summary>
    /// Gives the held seat to the person who accepted its invitation.
    /// </summary>
    internal async Task AssignInvitedAsync(AccountInvitation invitation, CancellationToken cancellationToken = default)
    {
        var seat = await _seats.FindByInvitationAsync(invitation.ItemId, cancellationToken)
            ?? (string.IsNullOrEmpty(invitation.SourceId) ? null : await _seats.FindByIdAsync(invitation.SourceId, cancellationToken));
        var pool = seat is null ? null : await FindAsync(seat.PoolId, cancellationToken);

        if (seat is null || pool is null || seat.State != LicenseSeatState.Invited || pool.State != LicensePoolState.Active)
        {
            return;
        }

        seat.UserId = invitation.AcceptedUserId;
        seat.State = LicenseSeatState.Assigned;
        seat.AssignedUtc = _clock.UtcNow;
        await _seats.UpdateAsync(seat, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
        await EnrollAsync(pool, seat, seat.GivenBy, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<LicenseSeatResult> RemoveAsync(string seatId, string actor, CancellationToken cancellationToken = default)
    {
        var seat = string.IsNullOrEmpty(seatId) ? null : await _seats.FindByIdAsync(seatId, cancellationToken);
        var pool = seat is null ? null : await FindAsync(seat.PoolId, cancellationToken);

        if (seat is null || pool is null || seat.State == LicenseSeatState.Removed)
        {
            return new LicenseSeatResult(null, S["The seat was not found or was already taken back."]);
        }

        await ReleaseSeatAsync(pool, seat, actor, cancellationToken);

        return new LicenseSeatResult(seat);
    }

    /// <inheritdoc/>
    public async Task<LicensePool> RevokeAsync(string poolId, string reason, CancellationToken cancellationToken = default)
    {
        var pool = await FindAsync(poolId, cancellationToken);

        if (pool is null || pool.State == LicensePoolState.Revoked)
        {
            return pool;
        }

        foreach (var seat in await _seats.ListByPoolAsync(pool.ItemId, cancellationToken))
        {
            if (seat.State == LicenseSeatState.Removed)
            {
                continue;
            }

            if (seat.State == LicenseSeatState.Assigned)
            {
                await _services.GetRequiredService<IEnrollmentService>().RevokeGrantAsync(LearningCommerceConstants.SeatSource, seat.ItemId, "system", reason, cancellationToken);
            }
            else if (!string.IsNullOrEmpty(seat.InvitationId))
            {
                await _services.GetRequiredService<IAccountInvitationService>().RevokeAsync(seat.InvitationId, "system", cancellationToken);
            }

            var fresh = await _seats.FindByIdAsync(seat.ItemId, cancellationToken);

            fresh.State = LicenseSeatState.Removed;
            fresh.RemovedUtc = _clock.UtcNow;
            await _seats.UpdateAsync(fresh, cancellationToken);
        }

        pool = await _pools.FindByIdAsync(poolId, cancellationToken);
        pool.State = LicensePoolState.Revoked;
        await _pools.UpdateAsync(pool, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return pool;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<LicenseSeat>> ListSeatsForPeopleAsync(IReadOnlyCollection<string> userIds, IReadOnlyCollection<string> emails, CancellationToken cancellationToken = default)
        => _seats.ListForPeopleAsync(
            (userIds ?? []).Where(id => !string.IsNullOrEmpty(id)).ToArray(),
            (emails ?? []).Where(email => !string.IsNullOrWhiteSpace(email)).Select(email => email.Trim().ToUpperInvariant()).ToArray(),
            cancellationToken);

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

    private async Task ReleaseSeatAsync(LicensePool pool, LicenseSeat seat, string actor, CancellationToken cancellationToken)
    {
        if (seat.State == LicenseSeatState.Assigned)
        {
            await _services.GetRequiredService<IEnrollmentService>().RevokeGrantAsync(LearningCommerceConstants.SeatSource, seat.ItemId, actor, S["The seat was taken back."], cancellationToken);
        }
        else if (!string.IsNullOrEmpty(seat.InvitationId))
        {
            await _services.GetRequiredService<IAccountInvitationService>().RevokeAsync(seat.InvitationId, actor, cancellationToken);
        }

        // Taking the enrollment back may commit the session: read the seat again before changing it.
        seat = await _seats.FindByIdAsync(seat.ItemId, cancellationToken);
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
            Actor = actor,
        }, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }

    private async Task EnrollAsync(LicensePool pool, LicenseSeat seat, string actor, CancellationToken cancellationToken)
    {
        await _services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = seat.UserId,
            CourseId = pool.CourseId,
            Source = LearningCommerceConstants.SeatSource,
            SourceId = seat.ItemId,
            AccessEndsUtc = pool.AccessDays is > 0 ? _clock.UtcNow.AddDays(pool.AccessDays.Value) : null,
            Actor = actor,
        }, cancellationToken);

        var observers = _services.GetServices<ILicensePoolObserver>().ToArray();

        foreach (var observer in observers)
        {
            await observer.SeatGivenAsync(pool, seat, cancellationToken);
        }

        var remaining = await GetRemainingAsync(pool, cancellationToken);

        if (pool.Seats > 0 && remaining * 10 <= pool.Seats)
        {
            foreach (var observer in observers)
            {
                await observer.RunningLowAsync(pool, remaining, cancellationToken);
            }
        }
    }
}

/// <summary>
/// Gives a held seat to the person who accepts its account invitation.
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
            ? ((DefaultLicensePoolService)_services.GetRequiredService<ILicensePoolService>()).AssignInvitedAsync(invitation, cancellationToken)
            : Task.CompletedTask;
}

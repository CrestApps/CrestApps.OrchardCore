using CrestApps.OrchardCore.Learning.Commerce.Models;

namespace CrestApps.OrchardCore.Learning.Commerce.Services;

/// <summary>
/// Licence pools: seats an account bought, given to members or to invited people, and taken back on a refund.
/// </summary>
public interface ILicensePoolService
{
    /// <summary>
    /// Creates the pool of an order line once; creating it again returns the same pool.
    /// </summary>
    Task<LicensePool> CreateAsync(LicensePool pool, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a pool.
    /// </summary>
    Task<LicensePool> FindAsync(string poolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the pools of an account.
    /// </summary>
    Task<IReadOnlyList<LicensePool>> ListForAccountAsync(string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets how many seats of a pool are left.
    /// </summary>
    Task<int> GetRemainingAsync(LicensePool pool, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the seats of a pool.
    /// </summary>
    Task<IReadOnlyList<LicenseSeat>> ListSeatsAsync(string poolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives a seat to a member of the account and enrolls them.
    /// </summary>
    Task<LicenseSeatResult> GiveAsync(string poolId, string userId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Holds a seat for an e-mail address and invites the person to the account; the seat is theirs when they accept.
    /// </summary>
    Task<LicenseSeatResult> InviteAsync(string poolId, string email, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a seat back: the person's enrollment from it ends and the seat returns to the pool.
    /// </summary>
    Task<LicenseSeatResult> RemoveAsync(string seatId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes every seat of a pool back and closes it, such as on a refund.
    /// </summary>
    Task<LicensePool> RevokeAsync(string poolId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the seats held by some people.
    /// </summary>
    Task<IReadOnlyList<LicenseSeat>> ListSeatsForPeopleAsync(IReadOnlyCollection<string> userIds, IReadOnlyCollection<string> emails, CancellationToken cancellationToken = default);
}

/// <summary>
/// The result of giving, inviting to or removing a seat.
/// </summary>
/// <param name="Seat">The seat.</param>
/// <param name="Error">Why it failed.</param>
public sealed record LicenseSeatResult(LicenseSeat Seat, string Error = null)
{
    /// <summary>
    /// Gets whether it worked.
    /// </summary>
    public bool Succeeded => Error is null && Seat is not null;
}

/// <summary>
/// Hears licence pools. Register one with <c>services.AddLicensePoolObserver&lt;T&gt;()</c>.
/// </summary>
public interface ILicensePoolObserver
{
    /// <summary>
    /// Called after an account bought seats.
    /// </summary>
    Task SeatsPurchasedAsync(LicensePool pool, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after a seat is given to a person.
    /// </summary>
    Task SeatGivenAsync(LicensePool pool, LicenseSeat seat, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called when a pool has 10 percent of its seats or fewer left, once per seat given below that.
    /// </summary>
    Task RunningLowAsync(LicensePool pool, int remaining, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

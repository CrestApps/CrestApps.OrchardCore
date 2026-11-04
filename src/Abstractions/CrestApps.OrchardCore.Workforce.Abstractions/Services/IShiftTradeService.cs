using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Lets workers give away or swap their published shifts. A colleague accepts, then a manager approves when the team
/// requires it. Both sides are checked by the <see cref="IShiftAssignmentValidator"/>s when the colleague accepts and
/// again when the trade is applied.
/// </summary>
public interface IShiftTradeService
{
    /// <summary>
    /// Offers a shift.
    /// </summary>
    /// <param name="offer">The offer.</param>
    /// <param name="actor">Who offered it, the worker or a manager on their behalf.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The trade, or why it was refused.</returns>
    Task<PlanningResult<ShiftTrade>> OfferAsync(ShiftTradeOffer offer, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a trade.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The trade, or <see langword="null"/>.</returns>
    Task<ShiftTrade> FindAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of trades, newest first.
    /// </summary>
    /// <param name="query">Which trades.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many match, and the page.</returns>
    Task<(int Count, IReadOnlyList<ShiftTrade> Items)> PageAsync(ShiftTradeQuery query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns why a worker can't accept a trade, for both sides of it; empty when they can.
    /// </summary>
    /// <param name="id">The trade.</param>
    /// <param name="workerId">The worker who would accept.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The problems.</returns>
    Task<IReadOnlyList<string>> CheckAsync(string id, string workerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts a trade as the colleague. Without manager approval the trade is applied at once.
    /// </summary>
    /// <param name="id">The trade.</param>
    /// <param name="workerId">The worker who accepts.</param>
    /// <param name="actor">Who accepted, the worker or someone on their behalf.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The trade, or why not.</returns>
    Task<PlanningResult<ShiftTrade>> AcceptAsync(string id, string workerId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns a trade down as the colleague it was offered to.
    /// </summary>
    /// <param name="id">The trade.</param>
    /// <param name="workerId">The colleague.</param>
    /// <param name="actor">Who turned it down.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The trade, or why not.</returns>
    Task<PlanningResult<ShiftTrade>> RejectAsync(string id, string workerId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves an accepted trade as a manager, which applies it.
    /// </summary>
    /// <param name="id">The trade.</param>
    /// <param name="actor">Who approved.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The trade, or why not.</returns>
    Task<PlanningResult<ShiftTrade>> ApproveAsync(string id, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Declines a trade as a manager.
    /// </summary>
    /// <param name="id">The trade.</param>
    /// <param name="note">Why, shown to the workers.</param>
    /// <param name="actor">Who declined.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The trade, or why not.</returns>
    Task<PlanningResult<ShiftTrade>> DeclineAsync(string id, string note, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes back an offer that isn't applied yet.
    /// </summary>
    /// <param name="id">The trade.</param>
    /// <param name="actor">Who took it back.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The trade, or why not.</returns>
    Task<PlanningResult<ShiftTrade>> CancelAsync(string id, string actor, CancellationToken cancellationToken = default);
}

using CrestApps.OrchardCore.Banking.Checks.Models;

namespace CrestApps.OrchardCore.Banking.Checks.Services;

/// <summary>
/// Is asked before a check is voided, reissued, cleared, un-cleared or edited (<see cref="CheckChangeKind.Updated"/>,
/// with the new values in <see cref="CheckChangingContext.Details"/>), before anything is saved, and may refuse the
/// change: the module the check pays reverses its payment in its own books on the void date, and refuses when it can't,
/// such as for a date in a closed accounting period. The <see cref="ICheckObserver"/>s are still told after the change
/// is saved. Register one with <see cref="ChecksServiceCollectionExtensions.AddCheckChangeHandler{THandler}"/>.
/// </summary>
public interface ICheckChangeHandler
{
    /// <summary>
    /// Called before a change is saved. Call <see cref="CheckChangingContext.Refuse"/> to stop it.
    /// </summary>
    /// <param name="context">The check and the change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    Task ChangingAsync(CheckChangingContext context, CancellationToken cancellationToken = default);
}

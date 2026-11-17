using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Gives the balances a pay statement shows, such as time off: a module that keeps them implements it, and a calculation
/// stores them on the statement as of its pay date.
/// </summary>
public interface IPayStatementBalanceSource
{
    /// <summary>
    /// Gets a staff member's balances.
    /// </summary>
    /// <param name="staffMemberId">The staff member.</param>
    /// <param name="date">The day, the statement's pay date.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The balances.</returns>
    Task<IReadOnlyList<PayStatementBalance>> GetBalancesAsync(string staffMemberId, DateOnly date, CancellationToken cancellationToken = default);
}

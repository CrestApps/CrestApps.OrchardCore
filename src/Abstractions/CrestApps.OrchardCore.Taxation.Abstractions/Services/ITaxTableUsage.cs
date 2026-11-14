using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Tells what uses a tax table, so it is not deleted while in use. Sales tax rules are checked by Taxation itself; a
/// module whose records point at tables (such as payroll tax rules) registers one.
/// </summary>
public interface ITaxTableUsage
{
    /// <summary>
    /// Describes what uses a table, or returns nothing.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What uses it.</returns>
    Task<IReadOnlyList<string>> GetUsagesAsync(TaxTable table, CancellationToken cancellationToken = default);
}

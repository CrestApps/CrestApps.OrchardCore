using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Manages payroll tax rules, so their handlers run.
/// </summary>
public interface IPayrollTaxRuleManager : ICatalogManager<PayrollTaxRule>
{
    /// <summary>
    /// Lists every rule.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The rules.</returns>
    Task<IReadOnlyList<PayrollTaxRule>> ListAsync(CancellationToken cancellationToken = default);
}

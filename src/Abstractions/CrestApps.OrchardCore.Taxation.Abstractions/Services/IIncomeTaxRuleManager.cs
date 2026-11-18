using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Manages income tax rules, so their handlers run.
/// </summary>
public interface IIncomeTaxRuleManager : ICatalogManager<IncomeTaxRule>
{
    /// <summary>
    /// Lists every rule.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The rules.</returns>
    Task<IReadOnlyList<IncomeTaxRule>> ListAsync(CancellationToken cancellationToken = default);
}

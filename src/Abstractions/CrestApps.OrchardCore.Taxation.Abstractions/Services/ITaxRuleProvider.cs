using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Resolves the tax rules that apply to a taxable item. The default implementation is backed by the
/// rule catalog, but third parties can supply their own resolution.
/// </summary>
public interface ITaxRuleProvider
{
    /// <summary>
    /// Gets the applicable rules for the supplied query, ordered deterministically.
    /// </summary>
    /// <param name="query">The criteria used to resolve rules.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The applicable rules.</returns>
    ValueTask<IReadOnlyList<TaxRule>> GetApplicableRulesAsync(TaxRuleQuery query, CancellationToken cancellationToken = default);
}

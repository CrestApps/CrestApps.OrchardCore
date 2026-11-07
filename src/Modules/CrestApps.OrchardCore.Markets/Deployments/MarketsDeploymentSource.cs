using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Markets.Models;
using CrestApps.OrchardCore.Markets.Recipes;
using CrestApps.OrchardCore.Markets.Services;

namespace CrestApps.OrchardCore.Markets.Deployments;

/// <summary>
/// Writes every market into a <c>Markets</c> recipe step, keyed by its id and its name.
/// </summary>
internal sealed class MarketsDeploymentSource : CatalogStoreDeploymentSource<MarketsDeploymentStep, Market>
{
    public MarketsDeploymentSource(IMarketStore store)
        : base(MarketsStep.EntriesName, store)
    {
    }

    protected override IEnumerable<Market> Order(IEnumerable<Market> entries)
        => entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
}

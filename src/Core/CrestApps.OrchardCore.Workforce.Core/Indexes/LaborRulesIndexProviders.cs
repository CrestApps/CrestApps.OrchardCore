using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The index providers of labor rules.
/// </summary>
public static class LaborRulesIndexProviders
{
    /// <summary>
    /// Creates one of each index provider.
    /// </summary>
    /// <returns>The providers.</returns>
    public static IIndexProvider[] All()
    {
        return
        [
            new LaborRuleSetIndexProvider(),
            new LaborRuleOverrideIndexProvider(),
            new PredictabilityPayEntryIndexProvider(),
        ];
    }
}

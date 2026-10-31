using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The index providers of shift planning.
/// </summary>
public static class ShiftPlanningIndexProviders
{
    /// <summary>
    /// Creates one of each index provider.
    /// </summary>
    /// <returns>The providers.</returns>
    public static IIndexProvider[] All()
    {
        return
        [
            new ShiftTemplateIndexProvider(),
            new ShiftPatternIndexProvider(),
            new CoverageRequirementIndexProvider(),
            new SchedulePeriodIndexProvider(),
            new ScheduleScenarioIndexProvider(),
            new DraftShiftIndexProvider(),
            new ScheduleChangeIndexProvider(),
        ];
    }
}

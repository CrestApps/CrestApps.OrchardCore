using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The index providers of time off.
/// </summary>
public static class TimeOffIndexProviders
{
    /// <summary>
    /// Creates one of each index provider.
    /// </summary>
    /// <returns>The providers.</returns>
    public static IIndexProvider[] All()
    {
        return
        [
            new TimeOffTypeIndexProvider(),
            new TimeOffPolicyIndexProvider(),
            new TimeOffPolicyAssignmentIndexProvider(),
            new TimeOffLedgerEntryIndexProvider(),
            new TimeOffRequestIndexProvider(),
            new TimeOffAllotmentIndexProvider(),
            new BlackoutPeriodIndexProvider(),
        ];
    }
}

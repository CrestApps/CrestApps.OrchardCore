using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The index providers of the Workforce collection.
/// </summary>
public static class WorkforceIndexProviders
{
    /// <summary>
    /// Creates one of each index provider.
    /// </summary>
    /// <returns>The providers.</returns>
    public static IIndexProvider[] All()
    {
        return
        [
            .. CrestApps.OrchardCore.Staff.Core.Indexes.StaffIndexProviders.All(),
            new ActivityCodeIndexProvider(),
        ];
    }
}

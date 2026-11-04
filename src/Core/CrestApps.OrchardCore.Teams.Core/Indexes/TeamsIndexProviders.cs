using YesSql.Indexes;

namespace CrestApps.OrchardCore.Teams.Core.Indexes;

/// <summary>
/// The index providers of the Teams collection, for the tests.
/// </summary>
public static class TeamsIndexProviders
{
    /// <summary>
    /// Creates one of each index provider.
    /// </summary>
    /// <returns>The providers.</returns>
    public static IIndexProvider[] All()
    {
        return
        [
            new TeamIndexProvider(),
        ];
    }
}

using YesSql.Indexes;

namespace CrestApps.OrchardCore.Skills.Core.Indexes;

/// <summary>
/// The index providers of the Skills collection, for the tests.
/// </summary>
public static class SkillsIndexProviders
{
    /// <summary>
    /// Creates one of each index provider.
    /// </summary>
    /// <returns>The providers.</returns>
    public static IIndexProvider[] All()
    {
        return
        [
            new SkillIndexProvider(),
            new CertificationIndexProvider(),
            new SkillAssignmentIndexProvider(),
            new CertificationRecordIndexProvider(),
        ];
    }
}

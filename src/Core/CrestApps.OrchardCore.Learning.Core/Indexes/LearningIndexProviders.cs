using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Core.Indexes;

/// <summary>
/// The index providers of the Learning collection, for the module and the tests.
/// </summary>
public static class LearningIndexProviders
{
    /// <summary>
    /// Creates one of each index provider of the learner records.
    /// </summary>
    /// <returns>The providers.</returns>
    public static IIndexProvider[] All()
        => [new EnrollmentIndexProvider(), new EnrollmentProgressIndexProvider(), new CourseEnrollmentKeyIndexProvider()];
}

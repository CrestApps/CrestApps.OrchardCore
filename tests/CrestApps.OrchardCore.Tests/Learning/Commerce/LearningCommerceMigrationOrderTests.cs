using CrestApps.OrchardCore.Learning.Commerce;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Tests.Learning.Commerce;

/// <summary>
/// Orchard Core runs a feature's migrations in the order they were registered and stops at the first one whose Create
/// returns 0 for the rest of the activation. The learning path products migration may return 0 (while the Learning Path
/// type does not exist), so it is registered after Learning Commerce's own migration; before, enabling every Learning
/// feature at once on a new site could leave the seats tables missing until a restart.
/// </summary>
public sealed class LearningCommerceMigrationOrderTests
{
    [Fact]
    public void TheLearningPathProductsStartup_RunsAfterTheLearningCommerceStartup()
        => Assert.True(new LearningPathsStartup().Order > new Startup().Order);

    [Fact]
    public void LearningCommercesOwnMigration_IsRegisteredBeforeTheOneThatMayWait()
    {
        // Arrange: the startups in the order Orchard Core calls them.
        var services = new ServiceCollection();
        var startups = new global::OrchardCore.Modules.IStartup[] { new LearningPathsStartup(), new Startup() }.OrderBy(startup => startup.Order);

        // Act
        foreach (var startup in startups)
        {
            startup.ConfigureServices(services);
        }

        // Assert
        var migrations = services.Where(descriptor => descriptor.ServiceType == typeof(IDataMigration)).Select(descriptor => descriptor.ImplementationType?.Name).ToArray();
        Assert.Equal(["LearningCommerceMigrations", "LearningPathProductMigrations"], migrations);
    }
}

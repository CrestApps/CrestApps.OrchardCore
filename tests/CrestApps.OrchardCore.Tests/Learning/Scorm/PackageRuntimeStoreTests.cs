using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Core.Indexes;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

public sealed class PackageRuntimeStoreTests
{
    [Fact]
    public async Task State_IsFoundByEnrollmentAndPackage_AndListedForItsLearner()
    {
        var (harness, _, _) = await LearningTestHarness.CreateAsync(
            services => services.AddScoped<PackageRuntimeStore>(),
            PackagesIndexSchema.CreateAsync,
            [new PackageRuntimeStateIndexProvider()],
            [PackagesConstants.CollectionName]);

        await using (harness)
        {
            await harness.RunAsync(async services =>
            {
                var store = services.GetRequiredService<PackageRuntimeStore>();

                await store.CreateAsync(new PackageRuntimeState { ItemId = "s1", EnrollmentId = "e1", UserId = "u1", PackageItemId = "p1", Cmi = new Scorm12Cmi { SuspendData = "page=3" } });
                await store.CreateAsync(new PackageRuntimeState { ItemId = "s2", EnrollmentId = "e1", UserId = "u1", PackageItemId = "p2" });
                await store.CreateAsync(new PackageRuntimeState { ItemId = "s3", EnrollmentId = "e2", UserId = "u2", PackageItemId = "p1" });

                return true;
            });

            var found = await harness.RunAsync(services => services.GetRequiredService<PackageRuntimeStore>().FindAsync("e1", "p1"));
            var fresh = await harness.RunAsync(services => services.GetRequiredService<PackageRuntimeStore>().FindFreshAsync("e2", "p1"));
            var missing = await harness.RunAsync(services => services.GetRequiredService<PackageRuntimeStore>().FindAsync("e2", "p2"));
            var listed = await harness.RunAsync(services => services.GetRequiredService<PackageRuntimeStore>().ListByUserAsync("u1"));

            Assert.Equal("s1", found.ItemId);
            Assert.Equal("page=3", found.Cmi.SuspendData);
            Assert.Equal("s3", fresh.ItemId);
            Assert.Null(missing);
            Assert.Equal(["s1", "s2"], listed.Select(state => state.ItemId).Order(StringComparer.Ordinal));
        }
    }
}

using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

public sealed class LearningPackageRuntimeStatesRecipeStepTests
{
    [Fact]
    public async Task ImportingTwice_KeepsOneStatePerEnrollmentAndPackage_AndSkipsUnknownLearners()
    {
        await using var h = await PackageTestHarness.CreateAsync();

        JsonObject Step(string status) => new()
        {
            ["name"] = PackagesConstants.RuntimeStatesRecipeStep,
            ["States"] = new JsonArray(
                new JsonObject
                {
                    ["ItemId"] = "4runtimeimport000000000000",
                    ["UserName"] = "ana",
                    ["EnrollmentId"] = "enrollment-ana",
                    ["CourseId"] = PackageTestHarness.Course,
                    ["PackageItemId"] = PackageTestHarness.Package,
                    ["Sessions"] = 2,
                    ["Cmi"] = new JsonObject { ["LessonStatus"] = status, ["ScoreRaw"] = "77", ["SuspendData"] = "p=3" },
                },
                new JsonObject
                {
                    ["UserName"] = "nobody",
                    ["EnrollmentId"] = "enrollment-x",
                    ["PackageItemId"] = PackageTestHarness.Package,
                }),
        };

        async Task<IList<string>> Import(string status)
        {
            var context = new RecipeExecutionContext { Name = PackagesConstants.RuntimeStatesRecipeStep, Step = Step(status) };

            await h.RunAsync(async services =>
            {
                await ActivatorUtilities.CreateInstance<LearningPackageRuntimeStatesRecipeStep>(services, new KnownUserLookup()).ExecuteAsync(context);

                return true;
            });

            return context.Errors;
        }

        var first = await Import("incomplete");
        var second = await Import("passed");
        var states = await h.RunAsync(services => services.GetRequiredService<PackageRuntimeStore>().ListByUserAsync("user-ana"));

        Assert.Single(first);
        Assert.Single(second);
        var state = Assert.Single(states);
        Assert.Equal("4runtimeimport000000000000", state.ItemId);
        Assert.Equal("passed", state.Cmi.LessonStatus);
        Assert.Equal("77", state.Cmi.ScoreRaw);
        Assert.Equal("p=3", state.Cmi.SuspendData);
        Assert.Equal(2, state.Sessions);
        Assert.Equal("enrollment-ana", state.EnrollmentId);
    }

    private sealed class KnownUserLookup : LearnerUserLookup
    {
        public KnownUserLookup()
            : base(null, null)
        {
        }

        public override Task<string> FindUserIdAsync(string userName)
            => Task.FromResult(userName is "ana" ? "user-ana" : null);
    }
}

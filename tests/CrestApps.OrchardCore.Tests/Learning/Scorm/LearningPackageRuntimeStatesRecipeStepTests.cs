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
        var enrollment = await h.EnrollAsync("user-ana");

        JsonObject Step(string status) => new()
        {
            ["name"] = PackagesConstants.RuntimeStatesRecipeStep,
            ["States"] = new JsonArray(
                new JsonObject
                {
                    ["ItemId"] = "4runtimeimport000000000000",
                    ["UserName"] = "ana",
                    ["EnrollmentId"] = enrollment.ItemId,
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
        Assert.Equal(enrollment.ItemId, state.EnrollmentId);
    }

    [Fact]
    public async Task Round9Low_AStateOfAnotherPersonsEnrollment_OrWithAValueOutsideTheStandard_IsSkipped()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var bobs = await h.EnrollAsync("user-bob");
        var anas = await h.EnrollAsync("user-ana");
        var context = new RecipeExecutionContext
        {
            Name = PackagesConstants.RuntimeStatesRecipeStep,
            Step = new JsonObject
            {
                ["name"] = PackagesConstants.RuntimeStatesRecipeStep,
                ["States"] = new JsonArray(
                    new JsonObject
                    {
                        ["UserName"] = "ana",
                        ["EnrollmentId"] = bobs.ItemId,
                        ["CourseId"] = PackageTestHarness.Course,
                        ["PackageItemId"] = PackageTestHarness.Package,
                        ["Cmi"] = new JsonObject { ["LessonStatus"] = "passed" },
                    },
                    new JsonObject
                    {
                        ["UserName"] = "ana",
                        ["EnrollmentId"] = anas.ItemId,
                        ["CourseId"] = PackageTestHarness.Course,
                        ["PackageItemId"] = PackageTestHarness.Package,
                        ["Cmi"] = new JsonObject { ["LessonStatus"] = new string('p', 300) },
                    }),
            },
        };

        await h.RunAsync(async services =>
        {
            await ActivatorUtilities.CreateInstance<LearningPackageRuntimeStatesRecipeStep>(services, new KnownUserLookup()).ExecuteAsync(context);

            return true;
        });

        Assert.Equal(2, context.Errors.Count);
        Assert.Empty(await h.RunAsync(services => services.GetRequiredService<PackageRuntimeStore>().ListByUserAsync("user-ana")));
    }

    [Fact]
    public async Task Scorm2004State_IsImportedWithItsStandardAndData()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("user-ana");
        var context = new RecipeExecutionContext
        {
            Name = PackagesConstants.RuntimeStatesRecipeStep,
            Step = new JsonObject
            {
                ["name"] = PackagesConstants.RuntimeStatesRecipeStep,
                ["States"] = new JsonArray(new JsonObject
                {
                    ["UserName"] = "ana",
                    ["EnrollmentId"] = enrollment.ItemId,
                    ["CourseId"] = PackageTestHarness.Course,
                    ["PackageItemId"] = PackageTestHarness.Package2004,
                    ["Standard"] = PackagesConstants.Standards.Scorm2004ThirdEdition,
                    ["Sessions"] = 3,
                    ["Attempts"] = 2,
                    ["Cmi2004"] = new JsonObject { ["Edition"] = 3, ["CompletionStatus"] = "completed", ["ScoreScaled"] = "0.7", ["Location"] = "p3", ["TotalTime"] = "PT1H0M0S" },
                }),
            },
        };

        await h.RunAsync(async services =>
        {
            await ActivatorUtilities.CreateInstance<LearningPackageRuntimeStatesRecipeStep>(services, new KnownUserLookup()).ExecuteAsync(context);

            return true;
        });

        var state = Assert.Single(await h.RunAsync(services => services.GetRequiredService<PackageRuntimeStore>().ListByUserAsync("user-ana")));

        Assert.Empty(context.Errors);
        Assert.Equal(PackagesConstants.Standards.Scorm2004ThirdEdition, state.Standard);
        Assert.Equal(2, state.Attempts);
        Assert.Equal("completed", state.Cmi2004.CompletionStatus);
        Assert.Equal("0.7", state.Cmi2004.ScoreScaled);
        Assert.Equal("p3", state.Cmi2004.Location);
        Assert.Equal(3, state.Cmi2004.Edition);
        Assert.Null(state.CurrentLaunchId);
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

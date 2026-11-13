using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Deployments;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents.Deployment;
using OrchardCore.Deployment;
using OrchardCore.Deployment.Services;
using OrchardCore.Lists.Models;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// A deployment plan exports the course, its lessons and its enrollments by user name; running the exported recipe puts
/// back what was removed, with the same ids, numbers and states.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class RecipeRoundTripTests
{
    private readonly LearningSiteFixture _fixture;

    public RecipeRoundTripTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AnExportedRecipe_RestoresTheCourseLessonsAndEnrollments()
    {
        // Arrange
        var original = await _fixture.InTenantAsync(async services => await services.GetRequiredService<EnrollmentStore>().FindByIdAsync(_fixture.EnrollmentId));
        var recipe = await _fixture.InTenantAsync(async services =>
        {
            var plan = new DeploymentPlan
            {
                Name = "Learning round trip",
                DeploymentSteps =
                [
                    new ContentDeploymentStep { ContentTypes = [LearningConstants.ContentTypes.Course, LearningConstants.ContentTypes.PageLesson, LearningConstants.ContentTypes.FileLesson] },
                    new LearningEnrollmentsDeploymentStep { IncludeProgress = true },
                ],
            };
            var result = new DeploymentPlanResult(new MemoryFileBuilder(), new RecipeDescriptor { Name = "LearningRoundTrip" });

            await services.GetRequiredService<IDeploymentManager>().ExecuteDeploymentPlanAsync(plan, result);
            await result.FinalizeAsync();

            return result.Recipe;
        });

        var enrollments = recipe["steps"]!.AsArray().OfType<JsonObject>().Single(step => step["name"]?.GetValue<string>() == LearningConstants.EnrollmentsRecipeStep);
        var row = Assert.Single(enrollments["Enrollments"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("learner1", row["UserName"]?.GetValue<string>());
        Assert.Null(row["UserId"]);

        var content = recipe["steps"]!.AsArray().OfType<JsonObject>().Single(step => string.Equals(step["name"]?.GetValue<string>(), "content", StringComparison.OrdinalIgnoreCase));
        var exported = content["data"]!.AsArray().OfType<JsonObject>().ToArray();
        Assert.Contains(exported, item => item["ContentItemId"]?.GetValue<string>() == _fixture.CourseId);
        Assert.Contains(exported, item => item["ContentItemId"]?.GetValue<string>() == _fixture.PageLessonId
            && item["ContainedPart"]?["ListContentItemId"]?.GetValue<string>() == _fixture.CourseId);

        // Act: remove the enrollment, then run the exported recipe.
        await _fixture.InTenantAsync(async services =>
        {
            var store = services.GetRequiredService<EnrollmentStore>();
            await store.DeleteAsync(await store.FindByIdAsync(_fixture.EnrollmentId));
        });

        Assert.Null(await _fixture.InTenantAsync(async services => await services.GetRequiredService<EnrollmentStore>().FindByIdAsync(_fixture.EnrollmentId)));

        await _fixture.InTenantAsync(services => LearningSiteFixture.RunRecipeAsync(services, recipe));

        // Assert
        var restored = await _fixture.InTenantAsync(async services => await services.GetRequiredService<EnrollmentStore>().FindByIdAsync(_fixture.EnrollmentId));
        Assert.NotNull(restored);
        Assert.Equal(original.Number, restored.Number);
        Assert.Equal(original.UserId, restored.UserId);
        Assert.Equal(original.Status, restored.Status);
        Assert.Equal(EnrollmentStatus.Active, restored.Status);

        var lesson = await _fixture.InTenantAsync(services => services.GetRequiredService<IContentManager>().GetAsync(_fixture.PageLessonId, VersionOptions.Published));
        Assert.NotNull(lesson);
        Assert.Equal(_fixture.CourseId, lesson.Get<ContainedPart>(nameof(ContainedPart))?.ListContentItemId);
    }

    private sealed class MemoryFileBuilder : IFileBuilder
    {
        public Task SetFileAsync(string subpath, Stream stream)
            => Task.CompletedTask;
    }
}

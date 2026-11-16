using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Lti;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Deployments;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.PersonalData;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Deployment;
using OrchardCore.Deployment.Services;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// External tools' data on a running site: the <c>LearningExternalTools</c> deployment step exports the tools' settings
/// with their subject salt and the recipe step imports them, so the ids a tool knows people by stay the same; personal
/// data exports a person's scores and the id each tool knows them by, and erasure removes the scores.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LtiDataTests
{
    private readonly LearningSiteFixture _fixture;

    public LtiDataTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Recipe_TheExportedStep_ImportsTheToolAgain_WithTheSameIds()
    {
        // Arrange: a tool of its own, so the shared one stays.
        var clientId = "lti-data-" + Guid.NewGuid().ToString("n");
        var (toolId, subject) = await _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<IExternalToolManager>();
            var tool = await manager.NewAsync();

            tool.Name = "Recipe tool";
            tool.ClientId = clientId;
            tool.DeploymentId = "deployment-recipe";
            tool.LoginInitiationUrl = "https://recipe.example.com/login";
            tool.TargetLinkUri = "https://recipe.example.com/launch";
            tool.PrivacyLevel = LtiPrivacyLevel.Name;
            tool.AllowGrades = true;

            await manager.CreateAsync(tool);

            return (tool.ItemId, LtiSubjects.For(tool, _fixture.EnrolledUserId));
        });

        var recipe = await _fixture.InTenantAsync(async services =>
        {
            var plan = new DeploymentPlan { Name = "External tools", DeploymentSteps = [new LearningExternalToolsDeploymentStep()] };
            var result = new DeploymentPlanResult(new MemoryFileBuilder(), new RecipeDescriptor { Name = "ExternalTools" });

            await services.GetRequiredService<IDeploymentManager>().ExecuteDeploymentPlanAsync(plan, result);
            await result.FinalizeAsync();

            return result.Recipe;
        });

        var step = recipe["steps"]!.AsArray().OfType<JsonObject>().Single(candidate => candidate["name"]?.GetValue<string>() == LtiConstants.RecipeStepName);
        var row = step["Tools"]!.AsArray().OfType<JsonObject>().Single(candidate => candidate["ClientId"]?.GetValue<string>() == clientId);

        // Act: remove the tool, then run the exported step.
        await _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<IExternalToolManager>();

            await manager.DeleteAsync(await manager.FindByIdAsync(toolId));
        });

        await _fixture.InTenantAsync(services => LearningSiteFixture.RunRecipeAsync(services, new JsonObject
        {
            ["name"] = "ExternalToolsImport",
            ["steps"] = new JsonArray(new JsonObject
            {
                ["name"] = LtiConstants.RecipeStepName,
                ["Tools"] = new JsonArray(row.DeepClone()),
            }),
        }));

        // Assert
        var imported = await _fixture.InTenantAsync(services => services.GetRequiredService<IExternalToolStore>().FindByClientIdAsync(clientId, TestContext.Current.CancellationToken));

        Assert.NotNull(imported);
        Assert.Equal(toolId, imported.ItemId);
        Assert.Equal("Recipe tool", imported.Name);
        Assert.Equal("deployment-recipe", imported.DeploymentId);
        Assert.Equal(LtiPrivacyLevel.Name, imported.PrivacyLevel);
        Assert.True(imported.AllowGrades);
        Assert.Equal(subject, LtiSubjects.For(imported, _fixture.EnrolledUserId));
    }

    [Fact]
    public async Task PersonalData_ExportsTheScoresAndTheIdsTools_KnowThePersonBy_AndErasureRemovesTheScores()
    {
        // Arrange: a score of a learner who is not the shared fixture's.
        await LtiLaunchTests.EnsureToolAndItemAsync(_fixture);

        var userId = await _fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, string.Concat("lti-data-", Guid.NewGuid().ToString("n").AsSpan(0, 8))));
        var subject = await _fixture.InTenantAsync(async services =>
        {
            var tool = await services.GetRequiredService<IExternalToolStore>().FindByClientIdAsync(LtiLaunchTests.ClientId, TestContext.Current.CancellationToken);

            await services.GetRequiredService<LtiScoreStore>().CreateAsync(new LtiScore
            {
                ItemId = IdGenerator.GenerateId(),
                LineItemId = "line-data",
                ToolId = tool.ItemId,
                CourseId = _fixture.CourseId,
                UserId = userId,
                Subject = LtiSubjects.For(tool, userId),
                ScoreGiven = 7,
                ScoreMaximum = 10,
                ActivityProgress = "Completed",
                GradingProgress = "FullyGraded",
                TimestampUtc = DateTime.UtcNow,
                ReceivedUtc = DateTime.UtcNow,
            });

            return LtiSubjects.For(tool, userId);
        });

        // Act
        var export = await _fixture.InTenantAsync(services => services.GetRequiredService<IPersonalDataService>().ExportAsync(new PersonalDataSubject(userId), TestContext.Current.CancellationToken));
        var erased = await _fixture.InTenantAsync(services => services.GetRequiredService<IPersonalDataService>().AnonymizeAsync(new PersonalDataSubject(userId), "erased", TestContext.Current.CancellationToken));
        var remaining = await _fixture.InTenantAsync(services => services.GetRequiredService<LtiScoreStore>().ListByUserAsync(userId, TestContext.Current.CancellationToken));

        // Assert
        var score = Assert.Single(export.Records, record => record.Kind == "LearningLtiScore");
        Assert.Equal("7", score.Fields["ScoreGiven"]);
        Assert.Contains(export.Records, record => record.Kind == "LearningLtiId" && record.Fields["Id"] == subject);
        Assert.Empty(remaining);
        Assert.Contains(erased.Warnings, warning => warning.Contains("External tools keep their own records", StringComparison.Ordinal));
    }

    private sealed class MemoryFileBuilder : IFileBuilder
    {
        public Task SetFileAsync(string subpath, Stream stream)
            => Task.CompletedTask;
    }
}

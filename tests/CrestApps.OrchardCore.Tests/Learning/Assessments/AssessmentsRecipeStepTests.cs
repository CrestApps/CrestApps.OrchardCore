using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Recipes;
using CrestApps.OrchardCore.Learning.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

public sealed class AssessmentsRecipeStepTests
{
    [Fact]
    public async Task ImportingGradeScalesTwice_KeepsOneScale_WithTheLatestBands()
    {
        // Arrange
        await using var h = await CreateAsync();

        JsonObject Step(string label) => new()
        {
            ["name"] = AssessmentsConstants.GradeScalesRecipeStep,
            ["GradeScales"] = new JsonArray(new JsonObject
            {
                ["ItemId"] = "4gradescale000000000000000",
                ["Name"] = "Letters",
                ["Bands"] = new JsonArray(
                    new JsonObject { ["MinPercent"] = 90, ["Label"] = label, ["Passing"] = true },
                    new JsonObject { ["MinPercent"] = 0, ["Label"] = "F", ["Passing"] = false }),
            }),
        };

        // Act
        var first = await Import<LearningGradeScalesRecipeStep>(h, Step("A"));
        var second = await Import<LearningGradeScalesRecipeStep>(h, Step("A+"));

        // Assert
        Assert.Empty(first);
        Assert.Empty(second);
        var scales = await h.RunAsync(async services => await services.GetRequiredService<GradeScaleStore>().GetAllAsync());
        var scale = Assert.Single(scales);
        Assert.Equal("A+", scale.Bands.First().Label);
    }

    [Fact]
    public async Task ImportingAttemptsTwice_KeepsOneAttemptAndOneOverride_AndTheAccessCodeWorks()
    {
        // Arrange
        await using var h = await CreateAsync();
        var hash = EnrollmentKeys.Hash("ROOM-7");

        JsonObject Step() => new()
        {
            ["name"] = AssessmentsConstants.AttemptsRecipeStep,
            ["AccessCodes"] = new JsonArray(new JsonObject { ["QuizId"] = "quiz", ["Hash"] = hash }),
            ["Attempts"] = new JsonArray(new JsonObject
            {
                ["ItemId"] = "4attemptimport000000000000",
                ["UserName"] = "learner1",
                ["QuizId"] = "quiz",
                ["CourseId"] = "course1",
                ["Number"] = "A-1",
                ["Status"] = "Graded",
                ["Score"] = 3,
                ["MaxScore"] = 4,
                ["GradedByUserName"] = "grader1",
            }),
            ["Overrides"] = new JsonArray(new JsonObject
            {
                ["ItemId"] = "4overrideimport00000000000",
                ["UserName"] = "learner1",
                ["QuizId"] = "quiz",
                ["ExtraMinutes"] = 15,
                ["Reason"] = "Reading support",
                ["ActorUserName"] = "manager1",
            }),
        };

        // Act
        var first = await Import<LearningAttemptsRecipeStep>(h, Step());
        var second = await Import<LearningAttemptsRecipeStep>(h, Step());

        // Assert
        Assert.Empty(first);
        Assert.Empty(second);
        var (attempts, overrides, codeWorks) = await h.RunAsync(async services => (
            await services.GetRequiredService<AssessmentAttemptStore>().ListAsync("user-learner1", "quiz"),
            await services.GetRequiredService<AssessmentOverrideStore>().ListByQuizAsync("quiz"),
            await services.GetRequiredService<LearningSecretStore>().VerifyAsync(AssessmentsConstants.QuizAccessCodeSecret, "quiz", "ROOM-7")));
        var attempt = Assert.Single(attempts);
        Assert.Equal("user-grader1", attempt.GradedBy);
        Assert.Equal("user-manager1", Assert.Single(overrides).Actor);
        Assert.True(codeWorks);
    }

    [Fact]
    public async Task AnAttemptOfAPersonNotOnTheSite_IsReportedAndSkipped()
    {
        // Arrange
        await using var h = await CreateAsync();
        var step = new JsonObject
        {
            ["name"] = AssessmentsConstants.AttemptsRecipeStep,
            ["Attempts"] = new JsonArray(new JsonObject { ["ItemId"] = "4attemptghost0000000000000", ["UserName"] = "ghost", ["QuizId"] = "quiz", ["Number"] = "A-9" }),
        };

        // Act
        var errors = await Import<LearningAttemptsRecipeStep>(h, step);

        // Assert
        Assert.Single(errors);
        Assert.Empty(await h.RunAsync(async services => await services.GetRequiredService<AssessmentAttemptStore>().ListAsync("user-ghost", "quiz")));
    }

    private static Task<AssessmentTestHarness> CreateAsync()
        => AssessmentTestHarness.CreateAsync(services =>
        {
            services.AddSingleton<LearnerUserLookup>(new NamedUsers());
            services.AddScoped<LearningGradeScalesRecipeStep>();
            services.AddScoped<LearningAttemptsRecipeStep>();
        });

    private static Task<IList<string>> Import<TStep>(AssessmentTestHarness h, JsonObject step)
        where TStep : global::OrchardCore.Recipes.Services.IRecipeStepHandler
        => h.RunAsync(async services =>
        {
            var context = new RecipeExecutionContext { Name = step["name"]!.GetValue<string>(), Step = step };

            await services.GetRequiredService<TStep>().ExecuteAsync(context);

            return context.Errors;
        });

    // Every user name is on the site as "user-{name}", except "ghost".
    private sealed class NamedUsers : LearnerUserLookup
    {
        public NamedUsers()
            : base(null, null)
        {
        }

        public override Task<string> FindUserIdAsync(string userName)
            => Task.FromResult(userName is null or "ghost" ? null : "user-" + userName);

        public override Task<IDictionary<string, string>> GetUserNamesAsync(IEnumerable<string> userIds)
            => Task.FromResult<IDictionary<string, string>>(userIds.Where(id => id is not null).Distinct().ToDictionary(id => id, id => id.Replace("user-", string.Empty, StringComparison.Ordinal), StringComparer.Ordinal));
    }
}

using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Recipes;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Learning;

public sealed class LearningEnrollmentsRecipeStepTests
{
    [Fact]
    public async Task ImportingTheSameStepTwice_KeepsOneEnrollmentAndOneProgressDocument()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync(services =>
        {
            services.AddSingleton<LearnerUserLookup>(new FixedUserLookup());
            services.AddScoped<LearningEnrollmentsRecipeStep>();
        });
        await using var __ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "intro");

        JsonObject Step() => new()
        {
            ["name"] = LearningConstants.EnrollmentsRecipeStep,
            ["Enrollments"] = new JsonArray(new JsonObject
            {
                ["ItemId"] = "4enrollmentimport000000000",
                ["UserName"] = "learner1",
                ["CourseId"] = "course1",
                ["Status"] = "Active",
                ["Number"] = "ENR-1",
                ["Progress"] = new JsonObject
                {
                    ["Items"] = new JsonArray(new JsonObject { ["ContentItemId"] = "intro", ["State"] = "Completed" }),
                },
            }),
        };

        async Task<IList<string>> Import()
        {
            var context = new RecipeExecutionContext { Name = LearningConstants.EnrollmentsRecipeStep, Step = Step() };

            await harness.RunAsync(services => services.GetRequiredService<LearningEnrollmentsRecipeStep>().ExecuteAsync(context));

            return context.Errors;
        }

        // Act
        var firstErrors = await Import();
        var secondErrors = await Import();

        // Assert
        Assert.Empty(firstErrors);
        Assert.Empty(secondErrors);
        var enrollments = await harness.RunAsync(services => services.GetRequiredService<EnrollmentStore>().ListByUserAsync("user-learner1"), commit: false);
        var enrollment = Assert.Single(enrollments);
        Assert.Equal("user-learner1", enrollment.UserId);
        var progress = await harness.RunAsync(services => services.GetRequiredService<EnrollmentProgressStore>().ListByEnrollmentsAsync([enrollment.ItemId]), commit: false);
        Assert.Single(progress);
    }

    private sealed class FixedUserLookup : LearnerUserLookup
    {
        public FixedUserLookup()
            : base(null, null)
        {
        }

        public override Task<string> FindUserIdAsync(string userName)
            => Task.FromResult(userName is null ? null : "user-" + userName);

        public override Task<IDictionary<string, string>> GetUserNamesAsync(IEnumerable<string> userIds)
            => Task.FromResult<IDictionary<string, string>>(userIds.ToDictionary(id => id, id => id.Replace("user-", string.Empty, StringComparison.Ordinal), StringComparer.Ordinal));
    }
}

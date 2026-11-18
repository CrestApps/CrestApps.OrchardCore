using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// SQLite keeps a decimal column's 100 as an integer and its 90.91 as a real, and a row reader built from the first row
/// cannot read the next. The certification upkeep's read of passed attempts must not depend on the first row's type.
/// </summary>
public sealed class AttemptMixedPercentTests
{
    [Fact]
    public async Task ListPassedUserIdsSince_ReadsAWholePercentAndAFractionalOne()
    {
        // Arrange
        await using var h = await CreateAsync();
        await AddPassedAttemptsAsync(h);

        // Act
        var users = await h.RunAsync(services => services.GetRequiredService<AssessmentAttemptStore>().ListPassedUserIdsSinceAsync("quiz", LearningTestHarness.Now.AddDays(-1)));

        // Assert
        Assert.Equal(["learner-a", "learner-b"], users.Order(StringComparer.Ordinal));
    }

    private static Task AddPassedAttemptsAsync(AssessmentTestHarness h)
        => h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<AssessmentAttemptStore>();
            var number = 0;

            foreach (var (userId, percent) in new[] { ("learner-a", 100m), ("learner-b", 90.91m) })
            {
                await store.CreateAsync(new AssessmentAttempt
                {
                    UserId = userId,
                    QuizId = "quiz",
                    CourseId = "course1",
                    Number = "A00000" + (++number),
                    Status = AttemptStatus.Graded,
                    StartedUtc = LearningTestHarness.Now.AddHours(-1),
                    SubmittedUtc = LearningTestHarness.Now,
                    Score = percent,
                    MaxScore = 100,
                    Percent = percent,
                    Passed = true,
                });
            }

            return true;
        });

    private static Task<AssessmentTestHarness> CreateAsync()
        => AssessmentTestHarness.CreateAsync();
}

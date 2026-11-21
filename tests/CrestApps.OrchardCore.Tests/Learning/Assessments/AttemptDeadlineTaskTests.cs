using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

public sealed class AttemptDeadlineTaskTests
{
    [Fact]
    public async Task TheDeadlineTask_SubmitsAnAttemptWhoseTimeIsUp_AndSettlesItsQueueItem()
    {
        // Arrange: the queue claims and settles items through sessions of its own, so the task's own session must not
        // hold an uncommitted write when it settles (on SQLite that failed with "database is locked").
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Questions.AddChoice("q1", "a");
        h.AddQuiz("course1", "quiz", quiz =>
        {
            quiz.QuestionIds = ["q1"];
            quiz.TimeLimitMinutes = 5;
        });
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;
        h.Clock.UtcNow = attempt.DeadlineUtc!.Value.AddSeconds(30);

        // Act
        await h.RunAsync(async services =>
        {
            await new AttemptDeadlineBackgroundTask().DoWorkAsync(services, CancellationToken.None);

            return true;
        });

        // Assert
        var stored = await h.RunAsync(async services => await services.GetRequiredService<AssessmentAttemptStore>().FindByIdAsync(attempt.ItemId));
        Assert.NotEqual(AttemptStatus.InProgress, stored.Status);
        Assert.True(stored.AutoSubmitted);
        Assert.Equal(attempt.DeadlineUtc, stored.SubmittedUtc);

        h.Clock.UtcNow = h.Clock.UtcNow.AddHours(1);
        var left = await h.RunAsync(services => services.GetRequiredService<ILeasedWorkQueue<AttemptDeadlineWork>>().ClaimDueAsync(10, TimeSpan.FromMinutes(1)));
        Assert.Empty(left);
    }
}

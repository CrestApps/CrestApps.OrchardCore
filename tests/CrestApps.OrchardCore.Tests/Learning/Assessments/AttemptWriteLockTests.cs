using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// Every write to an attempt runs under one lock per attempt, reads the attempt again inside it and commits before
/// letting it go, so a double submit grades once and a grade never overwrites a submit.
/// </summary>
public sealed class AttemptWriteLockTests
{
    [Fact]
    public async Task FourScopesSubmittingTheSameAttemptAtOnce_SubmitItOnce()
    {
        // Arrange
        var observer = new CountingAttemptObserver();
        await using var h = await AssessmentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IDistributedLock>();
            services.AddSingleton<IDistributedLock, KeyedLock>();
            services.AddSingleton<IAttemptObserver>(observer);
        });
        h.Courses.AddCourse("course1");
        h.Questions.AddChoice("q1", "a");
        h.AddQuiz("course1", "quiz", quiz => quiz.QuestionIds = ["q1"]);
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;

        // Act
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => h.RunAsync(service => service.SubmitAsync(attempt.ItemId, "learner")))));

        // Assert
        Assert.Equal(1, observer.Submitted);
        var stored = await h.RunAsync(async services => await services.GetRequiredService<AssessmentAttemptStore>().FindByIdAsync(attempt.ItemId));
        Assert.Single(stored.ScoreHistory, change => change.Kind == "Submitted");
    }

    [Fact]
    public async Task EveryAttemptWrite_TakesThePerAttemptLock()
    {
        // Arrange
        var locks = new RecordingLock();
        await using var h = await AssessmentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IDistributedLock>();
            services.AddSingleton<IDistributedLock>(locks);
        });
        h.Courses.AddCourse("course1");
        h.Questions.AddChoice("q1", "a");
        h.AddQuiz("course1", "quiz", quiz =>
        {
            quiz.QuestionIds = ["q1"];
            quiz.TimeLimitMinutes = 5;
        });
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;
        var key = "Learning:AttemptWrite:" + attempt.ItemId;
        var seen = new List<string>();

        async Task Record(string name, Func<IAssessmentAttemptService, Task<AttemptResult>> write)
        {
            locks.Keys.Clear();
            await h.RunAsync(write);

            if (locks.Keys.Contains(key))
            {
                seen.Add(name);
            }
        }

        // Act
        await Record("Save", service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["a"] }));
        await Record("Submit", service => service.SubmitAsync(attempt.ItemId, "learner"));
        await Record("Grade", service => service.GradeAsync(attempt.ItemId, new Dictionary<int, ManualScore>(), "Well done", "grader"));
        await Record("Regrade", service => service.RegradeAsync(attempt.ItemId, "grader", "Key fixed"));
        await Record("Void", service => service.VoidAsync(attempt.ItemId, "grader", "Cheating"));

        // Assert
        Assert.Equal(["Save", "Submit", "Grade", "Regrade", "Void"], seen);
    }

    private sealed class CountingAttemptObserver : IAttemptObserver
    {
        private int _submitted;

        public int Submitted => _submitted;

        public Task SubmittedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _submitted);

            return Task.CompletedTask;
        }
    }
}

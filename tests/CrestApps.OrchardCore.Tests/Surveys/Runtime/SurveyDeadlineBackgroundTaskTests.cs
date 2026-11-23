using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// The per-minute deadline task submits each response in a session of its own: one response whose save fails never
/// takes the others with it, and keeps its work item to be tried again (P3-H1).
/// </summary>
public sealed class SurveyDeadlineBackgroundTaskTests
{
    [Fact]
    public async Task DoWork_ASaveFailsForOneResponse_TheNextIsSubmittedAndNotifiedOnce_AndTheFailedOneKeepsItsWork()
    {
        // Arrange: two timed responses due in the same minute; the first one's completed save fails at flush time.
        var observer = new CompletedObserver();
        var poisonedId = string.Empty;
        var failure = new SaveFailureIndexProvider<SurveyResponse>(SurveysConstants.ResponsesCollectionName, response =>
            response.ItemId == poisonedId && response.Status != SurveyResponseStatus.InProgress ? new InvalidOperationException("index provider failed") : null);
        await using var harness = await RuntimeTestHarness.CreateAsync(services =>
        {
            SaveFailureIndexProvider<SurveyResponse>.Register(services, failure);
            services.AddSingleton<ISurveyResponseObserver>(observer);
        });
        var store = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<IStore>()));
        await SaveFailureIndexProvider<SurveyResponse>.CreateTableAsync(store, SurveysConstants.ResponsesCollectionName);

        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));
        definition.Timing.TimeLimitMinutes = 1;

        var survey = await harness.PublishAsync(definition);
        var poisoned = (await harness.StartAsync(survey, "u1")).Response;
        var healthy = (await harness.StartAsync(survey, "u2")).Response;

        poisonedId = poisoned.ItemId;
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddMinutes(5);

        // Act
        await harness.RunInShellAsync(async services =>
        {
            await new SurveyDeadlineBackgroundTask().DoWorkAsync(services, TestContext.Current.CancellationToken);

            return true;
        });

        // Assert
        var work = await harness.RunAsync(async services => (await services.GetRequiredService<ISession>()
            .Query<SurveyDeadlineWork, LeasedWorkItemIndex>(collection: SurveyDeadlineWork.CollectionName)
            .ListAsync(TestContext.Current.CancellationToken))
            .Select(item => item.ResponseId)
            .ToArray());

        Assert.Equal(SurveyResponseStatus.Completed, (await harness.FindAsync(healthy.ItemId)).Status);
        Assert.Equal(SurveyResponseStatus.InProgress, (await harness.FindAsync(poisoned.ItemId)).Status);
        Assert.Equal([healthy.ItemId], observer.Completed);
        Assert.Equal([poisoned.ItemId], work);
    }

    private sealed class CompletedObserver : ISurveyResponseObserver
    {
        public List<string> Completed { get; } = [];

        public Task CompletedAsync(SurveyResponseEventContext context)
        {
            Completed.Add(context.Response.ItemId);

            return Task.CompletedTask;
        }
    }
}

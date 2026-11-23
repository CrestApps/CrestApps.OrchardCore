using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// The hourly upkeep: participation records are deleted until it catches up, anonymize retention removes the files, a
/// response that keeps failing stops being tried so it can't block the rest, and a response whose save fails never takes
/// the other responses' changes with it (R4-H1).
/// </summary>
public sealed class SurveyMaintenanceTests
{
    [Fact]
    public async Task Run_MoreExpiredParticipationRecordsThanABatch_DeletesThemAll()
    {
        // Arrange: 12 records older than the participation retention, and one recent record that stays.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var today = DateOnly.FromDateTime(RuntimeTestHarness.Now);

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyParticipationStore>();

            for (var index = 0; index < 12; index++)
            {
                await store.CreateAsync(new SurveyParticipation { SurveyId = "s1", RespondentKey = "k" + index, Day = today.AddDays(-SurveyParticipationService.RetentionDays - 5), StartedCount = 1 });
            }

            await store.CreateAsync(new SurveyParticipation { SurveyId = "s1", RespondentKey = "recent", Day = today, StartedCount = 1 });

            return true;
        });

        // Act: a batch of 5 used to delete only 5 records an hour.
        await RunMaintenanceAsync(harness, batchSize: 5);

        // Assert
        var left = await harness.RunAsync(services => services.GetRequiredService<ISession>()
            .Query<SurveyParticipation, SurveyParticipationIndex>(collection: SurveysConstants.CollectionName)
            .ListAsync(TestContext.Current.CancellationToken));

        Assert.Equal(["recent"], left.Select(row => row.RespondentKey));
    }

    [Fact]
    public async Task Run_AnonymizeRetention_RemovesTheResponsesFiles()
    {
        // Arrange
        var files = new SurveyFilesFakeAttachmentService();
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSingleton<IAttachmentService>(files));
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));
        var survey = await harness.PublishAsync(definition, item => item.Retention = new SurveyRetentionSettings { Days = 30, Action = RetentionAction.Anonymize });
        var identity = new SurveyRespondentIdentity { UserId = "u1" };
        var response = (await harness.StartAsync(survey, "u1")).Response;

        await harness.PostAsync(response, identity, SurveyPageDirection.Finish, ("q1", "a"));

        // A signature or upload the response owns, which names its uploader.
        var signature = files.Seed(SurveyResponseFiles.OwnerOf(response.ItemId), RuntimeTestHarness.Now);
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyResponseStore>();
            var stored = await store.FindByIdAsync(response.ItemId);

            stored.Answers["sig"] = new SurveyAnswer { AttachmentIds = [signature.ItemId] };
            await store.UpdateAsync(stored);

            return true;
        });

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddDays(40);
        await RunMaintenanceAsync(harness);

        // Assert
        var anonymized = await harness.FindAsync(response.ItemId);

        Assert.Null(anonymized.UserId);
        Assert.NotNull(anonymized.AnonymizedUtc);
        Assert.Empty(anonymized.Answers["sig"].AttachmentIds);
        Assert.Equal("a", anonymized.Answers["q1"].Values.Single());
        Assert.Empty(files.Stored);
    }

    [Fact]
    public async Task Run_AnonymizedResponses_AreNotVisitedAgain_SoTheBatchReachesNewerOnes()
    {
        // Arrange: two old responses, a batch of one.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));
        var survey = await harness.PublishAsync(definition, item => item.Retention = new SurveyRetentionSettings { Days = 30, Action = RetentionAction.Anonymize });
        var first = (await harness.StartAsync(survey, "u1")).Response;

        await harness.PostAsync(first, new SurveyRespondentIdentity { UserId = "u1" }, SurveyPageDirection.Finish, ("q1", "a"));

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddMinutes(5);
        var second = (await harness.StartAsync(survey, "u2")).Response;

        await harness.PostAsync(second, new SurveyRespondentIdentity { UserId = "u2" }, SurveyPageDirection.Finish, ("q1", "b"));

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddDays(40);
        await RunMaintenanceAsync(harness, batchSize: 1);
        await RunMaintenanceAsync(harness, batchSize: 1);

        // Assert
        Assert.Null((await harness.FindAsync(first.ItemId)).UserId);
        Assert.Null((await harness.FindAsync(second.ItemId)).UserId);
    }

    [Fact]
    public async Task Run_AResponseThatKeepsFailing_StopsBeingTried_AndNoLongerBlocksTheBatch()
    {
        // Arrange: a poisoned overdue response first in line, a healthy one behind it, and a batch of one.
        var engine = new PoisonScoringEngine();
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSingleton<ISurveyScoringEngine>(engine));
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));
        definition.Timing.TimeLimitMinutes = 1;
        definition.Scoring.Enabled = true;

        var survey = await harness.PublishAsync(definition);
        var poisoned = (await harness.StartAsync(survey, "u1")).Response;

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddSeconds(30);
        var healthy = (await harness.StartAsync(survey, "u2")).Response;

        engine.PoisonedResponseId = poisoned.ItemId;
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddMinutes(10);

        // Act
        for (var run = 0; run < SurveyMaintenanceService.MaxUpkeepFailures; run++)
        {
            await RunMaintenanceAsync(harness, batchSize: 1);
        }

        var blocked = await harness.FindAsync(healthy.ItemId);

        await RunMaintenanceAsync(harness, batchSize: 1);

        // Assert
        var failed = await harness.FindAsync(poisoned.ItemId);

        Assert.Equal(SurveyResponseStatus.InProgress, blocked.Status);
        Assert.Equal(SurveyResponseStatus.InProgress, failed.Status);
        Assert.Equal(SurveyMaintenanceService.MaxUpkeepFailures, failed.UpkeepFailures);
        Assert.Equal(SurveyResponseStatus.Completed, (await harness.FindAsync(healthy.ItemId)).Status);
    }

    [Fact]
    public async Task Run_ASaveFailsForOneResponse_TheNextResponseIsStillSaved_AndNotifiedOnce()
    {
        // Arrange: two idle responses; the older one's save fails at flush time once it is abandoned.
        var observer = new RecordingObserver();
        var poisonedId = string.Empty;
        var failure = new SaveFailureIndexProvider<SurveyResponse>(SurveysConstants.ResponsesCollectionName, response =>
            response.ItemId == poisonedId && response.Status == SurveyResponseStatus.Abandoned ? new InvalidOperationException("index provider failed") : null);
        await using var harness = await CreateWithSaveFailureAsync(failure, services => services.AddSingleton<ISurveyResponseObserver>(observer));
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var poisoned = (await harness.StartAsync(survey, "u1")).Response;

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddSeconds(30);
        var healthy = (await harness.StartAsync(survey, "u2")).Response;

        poisonedId = poisoned.ItemId;
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddDays(10);

        // Act
        await RunMaintenanceAsync(harness);

        // Assert
        var failed = await harness.FindAsync(poisoned.ItemId);

        Assert.Equal(SurveyResponseStatus.Abandoned, (await harness.FindAsync(healthy.ItemId)).Status);
        Assert.Equal([healthy.ItemId], observer.Abandoned);
        Assert.Equal(SurveyResponseStatus.InProgress, failed.Status);
        Assert.Equal(1, failed.UpkeepFailures);
    }

    [Fact]
    public async Task Run_AnAnonymizeSaveFails_KeepsThatResponsesFiles_AndStillAnonymizesTheNext()
    {
        // Arrange: two old responses with a file each; the older one's anonymized save fails at flush time.
        var files = new SurveyFilesFakeAttachmentService();
        var poisonedId = string.Empty;
        var failure = new SaveFailureIndexProvider<SurveyResponse>(SurveysConstants.ResponsesCollectionName, response =>
            response.ItemId == poisonedId && response.AnonymizedUtc is not null ? new InvalidOperationException("index provider failed") : null);
        await using var harness = await CreateWithSaveFailureAsync(failure, services => services.AddSingleton<IAttachmentService>(files));
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))), item => item.Retention = new SurveyRetentionSettings { Days = 30, Action = RetentionAction.Anonymize });
        var first = await CompleteWithFileAsync(harness, files, survey, "u1");

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddMinutes(5);
        var second = await CompleteWithFileAsync(harness, files, survey, "u2");

        poisonedId = first.Response.ItemId;
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddDays(40);

        // Act
        await RunMaintenanceAsync(harness);

        // Assert
        var failed = await harness.FindAsync(first.Response.ItemId);
        var anonymized = await harness.FindAsync(second.Response.ItemId);

        Assert.Equal("u1", failed.UserId);
        Assert.Equal(1, failed.UpkeepFailures);
        Assert.True(files.Stored.ContainsKey(first.FileId));
        Assert.Null(anonymized.UserId);
        Assert.Null(anonymized.RespondentKey);
        Assert.NotNull(anonymized.AnonymizedUtc);
        Assert.False(files.Stored.ContainsKey(second.FileId));
    }

    [Fact]
    public async Task Run_AStepThatSucceedsAfterEarlierFailures_ClearsTheCount()
    {
        // Arrange: an idle response whose abandon fails at save time for two runs.
        var poisonedId = string.Empty;
        var failing = true;
        var failure = new SaveFailureIndexProvider<SurveyResponse>(SurveysConstants.ResponsesCollectionName, response =>
            failing && response.ItemId == poisonedId && response.Status == SurveyResponseStatus.Abandoned ? new InvalidOperationException("index provider failed") : null);
        await using var harness = await CreateWithSaveFailureAsync(failure);
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var response = (await harness.StartAsync(survey, "u1")).Response;

        poisonedId = response.ItemId;
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddDays(10);

        await RunMaintenanceAsync(harness);
        await RunMaintenanceAsync(harness);
        var afterFailures = await harness.FindAsync(response.ItemId);

        // Act
        failing = false;
        await RunMaintenanceAsync(harness);

        // Assert
        var after = await harness.FindAsync(response.ItemId);

        Assert.Equal(2, afterFailures.UpkeepFailures);
        Assert.Equal(SurveyResponseStatus.Abandoned, after.Status);
        Assert.Equal(0, after.UpkeepFailures);
    }

    [Fact]
    public async Task Run_AFailureFromTheServersEnvironment_IsNotCounted()
    {
        // Arrange: the anonymity secret can't be read on this server.
        var poisonedId = string.Empty;
        var failure = new SaveFailureIndexProvider<SurveyResponse>(SurveysConstants.ResponsesCollectionName, response =>
            response.ItemId == poisonedId && response.Status == SurveyResponseStatus.Abandoned ? new SurveyAnonymitySecretUnreadableException() : null);
        await using var harness = await CreateWithSaveFailureAsync(failure);
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var response = (await harness.StartAsync(survey, "u1")).Response;

        poisonedId = response.ItemId;
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddDays(10);

        // Act
        await RunMaintenanceAsync(harness);

        // Assert
        var after = await harness.FindAsync(response.ItemId);

        Assert.Equal(SurveyResponseStatus.InProgress, after.Status);
        Assert.Equal(0, after.UpkeepFailures);
    }

    [Fact]
    public async Task ClearUpkeepFailures_LetsUpkeepTryAStuckResponseAgain()
    {
        // Arrange: a response upkeep stopped trying.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var response = (await harness.StartAsync(survey, "u1")).Response;

        for (var failure = 0; failure < SurveyMaintenanceService.MaxUpkeepFailures; failure++)
        {
            await harness.RunAsync(services => services.GetRequiredService<SurveyResponseService>().CountUpkeepFailureAsync(response.ItemId, TestContext.Current.CancellationToken));
        }

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddDays(10);
        await RunMaintenanceAsync(harness);
        var skipped = await harness.FindAsync(response.ItemId);

        // Act
        await harness.RunAsync(services => services.GetRequiredService<SurveyResponseService>().ClearUpkeepFailuresAsync(response.ItemId, TestContext.Current.CancellationToken));
        await RunMaintenanceAsync(harness);

        // Assert
        Assert.Equal(SurveyResponseStatus.InProgress, skipped.Status);
        Assert.Equal(SurveyResponseStatus.Abandoned, (await harness.FindAsync(response.ItemId)).Status);
    }

    [Fact]
    public void Anonymize_ClearsTheRespondentKey()
    {
        var response = new SurveyResponse { RespondentKey = "u:u1" };

        Assert.True(SurveyMaintenanceService.Anonymize(response));
        Assert.Null(response.RespondentKey);
    }

    private static async Task<RuntimeTestHarness> CreateWithSaveFailureAsync(SaveFailureIndexProvider<SurveyResponse> failure, Action<IServiceCollection> configure = null)
    {
        var harness = await RuntimeTestHarness.CreateAsync(services =>
        {
            SaveFailureIndexProvider<SurveyResponse>.Register(services, failure);
            configure?.Invoke(services);
        });

        var store = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<IStore>()));
        await SaveFailureIndexProvider<SurveyResponse>.CreateTableAsync(store, SurveysConstants.ResponsesCollectionName);

        return harness;
    }

    private static async Task<(SurveyResponse Response, string FileId)> CompleteWithFileAsync(RuntimeTestHarness harness, SurveyFilesFakeAttachmentService files, Survey survey, string userId)
    {
        var response = (await harness.StartAsync(survey, userId)).Response;

        await harness.PostAsync(response, new SurveyRespondentIdentity { UserId = userId }, SurveyPageDirection.Finish, ("q1", "a"));

        var file = files.Seed(SurveyResponseFiles.OwnerOf(response.ItemId), RuntimeTestHarness.Now);
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyResponseStore>();
            var stored = await store.FindByIdAsync(response.ItemId);

            stored.Answers["sig"] = new SurveyAnswer { AttachmentIds = [file.ItemId] };
            await store.UpdateAsync(stored);

            return true;
        });

        return (response, file.ItemId);
    }

    private static Task<bool> RunMaintenanceAsync(RuntimeTestHarness harness, int batchSize = SurveysConstants.PageSize)
        => harness.RunInShellAsync(async services =>
        {
            await services.GetRequiredService<SurveyMaintenanceService>().RunAsync(batchSize, TestContext.Current.CancellationToken);

            return true;
        });

    private sealed class RecordingObserver : ISurveyResponseObserver
    {
        public List<string> Abandoned { get; } = [];

        public Task AbandonedAsync(SurveyResponseEventContext context)
        {
            Abandoned.Add(context.Response.ItemId);

            return Task.CompletedTask;
        }
    }

    private sealed class PoisonScoringEngine : ISurveyScoringEngine
    {
        public string PoisonedResponseId { get; set; }

        public void Score(SurveyDefinition definition, SurveyResponse response)
        {
            if (response.ItemId == PoisonedResponseId)
            {
                throw new OverflowException("Value was either too large or too small for a Decimal.");
            }

            response.Grading = GradingStatus.Graded;
        }
    }
}

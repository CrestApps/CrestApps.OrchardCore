using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Surveys;
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
/// The hourly upkeep: participation records are deleted until it catches up, anonymize retention removes the files, and
/// a response that keeps failing stops being tried so it can't block the rest.
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

    private static Task<bool> RunMaintenanceAsync(RuntimeTestHarness harness, int batchSize = SurveysConstants.PageSize)
        => harness.RunAsync(async services =>
        {
            await services.GetRequiredService<SurveyMaintenanceService>().RunAsync(batchSize, TestContext.Current.CancellationToken);

            return true;
        });

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

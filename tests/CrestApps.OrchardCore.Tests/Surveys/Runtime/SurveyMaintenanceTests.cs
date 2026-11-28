using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Controls.ContactInfo;
using CrestApps.OrchardCore.Surveys.Core.Controls.Hidden;
using CrestApps.OrchardCore.Surveys.Core.Controls.Signature;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Dapper;
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
    public async Task Run_MoreKeptRecordsThanARunReads_GoesOnFromWhereTheLastRunStopped_AndReachesTheExpiredRecordsAfterThem()
    {
        // Arrange: two surveys with a total limit keep their records, and too many surveys keep records to leave them out
        // of the query (the limit is lowered to 1). Their 6 old records hold the lowest ids; 2 expired records of a survey
        // without a total come after them. A run reads one batch of 2.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var old = DateOnly.FromDateTime(RuntimeTestHarness.Now).AddDays(-SurveyParticipationService.RetentionDays - 5);
        var keptA = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Choice("q1", "a"))), item => item.Access.Limits = new RespondentLimits { Total = 1 });
        var keptB = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Choice("q1", "a"))), item => item.Access.Limits = new RespondentLimits { Total = 1 });

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyParticipationStore>();

            for (var index = 0; index < 6; index++)
            {
                await store.CreateAsync(new SurveyParticipation { SurveyId = index % 2 == 0 ? keptA.ItemId : keptB.ItemId, RespondentKey = "kept" + index, Day = old, CompletedCount = 1 });
            }

            await store.CreateAsync(new SurveyParticipation { SurveyId = "other", RespondentKey = "expired1", Day = old, StartedCount = 1 });
            await store.CreateAsync(new SurveyParticipation { SurveyId = "other", RespondentKey = "expired2", Day = old, StartedCount = 1 });

            return true;
        });

        // Act: four runs. Each used to start over at the oldest record and read only kept ones.
        for (var run = 0; run < 4; run++)
        {
            await harness.RunInShellAsync(async services =>
            {
                var maintenance = services.GetRequiredService<SurveyMaintenanceService>();
                maintenance.ParticipationBatchLimit = 1;
                maintenance.KeptSurveysInQueryLimit = 1;
                await maintenance.RunAsync(2, TestContext.Current.CancellationToken);

                return true;
            });
        }

        // Assert: the expired records are gone, the kept ones stay.
        var left = await harness.RunAsync(services => services.GetRequiredService<ISession>()
            .Query<SurveyParticipation, SurveyParticipationIndex>(collection: SurveysConstants.CollectionName)
            .ListAsync(TestContext.Current.CancellationToken));

        Assert.Equal(6, left.Count);
        Assert.All(left, row => Assert.StartsWith("kept", row.RespondentKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Run_AfterASurveyWasDeleted_RemovesWhatALateSendWroteForIt_ThenForgetsTheDelete()
    {
        // Arrange: a survey deleted two hours ago, and an invitation a send that was already running saved afterwards.
        await using var harness = await RuntimeTestHarness.CreateAsync();

        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            await session.SaveAsync(new SurveyDeletion { SurveyId = "gone", StartedUtc = RuntimeTestHarness.Now.AddHours(-2) }, false, SurveysConstants.CollectionName, TestContext.Current.CancellationToken);
            await services.GetRequiredService<SurveyInvitationStore>().CreateAsync(new SurveyInvitation { ItemId = "late", SurveyId = "gone", CollectorId = "c", Email = "late@example.com" });

            return true;
        });

        // Act: the first run removes the invitation, the next one finds nothing left and forgets the delete.
        await RunMaintenanceAsync(harness);
        var notesAfterFirst = await CountAsync<SurveyDeletion>(harness);
        await RunMaintenanceAsync(harness);

        // Assert
        Assert.Null(await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().FindByIdAsync("late").AsTask()));
        Assert.Equal(1, notesAfterFirst);
        Assert.Equal(0, await CountAsync<SurveyDeletion>(harness));
    }

    private static Task<int> CountAsync<T>(RuntimeTestHarness harness)
        where T : class
        => harness.RunAsync(async services => (await services.GetRequiredService<ISession>().Query<T>(SurveysConstants.CollectionName).ListAsync(TestContext.Current.CancellationToken)).Count);

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
    public async Task Run_AnonymizeRetention_RemovesPersonalAnswersAndEverythingThatPointsBackToTheRespondent()
    {
        // Arrange: R12-M2, a completed response with contact details, a signature kept in the answer, a hidden value
        // copied from a variable, and the variables, properties, context, invitation seal and resume token hash.
        await using var harness = await RuntimeTestHarness.CreateAsync(services =>
        {
            services.AddSurveyControl<ContactInfoControlHandler>(SurveyControlNames.ContactInfo, descriptor => descriptor.Capabilities = SurveyControlCapabilities.Answerable);
            services.AddSurveyControl<SignatureControlHandler>(SurveyControlNames.Signature, descriptor => descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.StoresFiles);
            services.AddSurveyControl<HiddenControlHandler>(SurveyControlNames.Hidden, descriptor => descriptor.Capabilities = SurveyControlCapabilities.Answerable);
        });
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page(
            "p1",
            RuntimeSurveys.Text("q1"),
            new SurveyQuestion { Id = "contact", Name = "contact", ControlType = SurveyControlNames.ContactInfo },
            new SurveyQuestion { Id = "sign", Name = "sign", ControlType = SurveyControlNames.Signature },
            new SurveyQuestion { Id = "hidden", Name = "email", ControlType = SurveyControlNames.Hidden }));
        var survey = await harness.PublishAsync(definition, item => item.Retention = new SurveyRetentionSettings { Days = 30, Action = RetentionAction.Anonymize });
        var response = (await harness.StartAsync(survey, "u1")).Response;

        await harness.PostAsync(response, new SurveyRespondentIdentity { UserId = "u1" }, SurveyPageDirection.Finish, ("q1", "a"));
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyResponseStore>();
            var stored = await store.FindByIdAsync(response.ItemId);
            var signature = new SurveyAnswer();
            signature.Extra[SignatureControlHandler.DataUrlKey] = "data:image/png;base64,iVBORw0KGgo=";

            stored.Answers["contact"] = new SurveyAnswer { Rows = { ["email"] = ["pat@example.com"], ["name"] = ["Pat"] } };
            stored.Answers["sign"] = signature;
            stored.Answers["hidden"] = new SurveyAnswer { Values = ["pat@example.com"] };
            stored.Variables["email"] = "pat@example.com";
            stored.Properties["crm"] = "contact-1";
            stored.Context = new CrestApps.OrchardCore.Records.RecordRef("Contact", "contact-1");
            stored.InvitationSeal = "seal";
            stored.ResumeTokenHash = "hash";
            await store.UpdateAsync(stored);

            return true;
        });

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddDays(40);
        await RunMaintenanceAsync(harness);

        // Assert
        var anonymized = await harness.FindAsync(response.ItemId);

        Assert.NotNull(anonymized.AnonymizedUtc);
        Assert.Equal("a", anonymized.Answers["q1"].Values.Single());
        Assert.False(anonymized.Answers.ContainsKey("contact"));
        Assert.False(anonymized.Answers.ContainsKey("sign"));
        Assert.False(anonymized.Answers.ContainsKey("hidden"));
        Assert.Empty(anonymized.Variables);
        Assert.Empty(anonymized.Properties);
        Assert.Null(anonymized.Context);
        Assert.Null(anonymized.InvitationSeal);
        Assert.Null(anonymized.ResumeTokenHash);
    }

    [Fact]
    public async Task Run_AnonymizeRetention_ClearsWhenTheRespondentAcceptedTheStatement()
    {
        // Arrange: a completed response whose respondent accepted the survey's statement; the time they accepted it is
        // theirs, as the time they started is.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));
        var survey = await harness.PublishAsync(definition, item => item.Retention = new SurveyRetentionSettings { Days = 30, Action = RetentionAction.Anonymize });
        var response = (await harness.StartAsync(survey, "u1")).Response;

        await harness.PostAsync(response, new SurveyRespondentIdentity { UserId = "u1" }, SurveyPageDirection.Finish, ("q1", "a"));
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyResponseStore>();
            var stored = await store.FindByIdAsync(response.ItemId);

            stored.StatementAcceptedUtc = RuntimeTestHarness.Now;
            await store.UpdateAsync(stored);

            return true;
        });

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddDays(40);
        await RunMaintenanceAsync(harness);

        // Assert
        var anonymized = await harness.FindAsync(response.ItemId);

        Assert.NotNull(anonymized.AnonymizedUtc);
        Assert.Null(anonymized.StatementAcceptedUtc);
    }

    [Fact]
    public async Task Run_ReadsOnlyTheSurveysItsStepsNeed_AndASurveyIndexedBeforeTheUpkeepColumnsStillGetsRetention()
    {
        // Arrange: a survey with retention; one indexed before the upkeep columns existed (empty retention days) whose
        // retention is only in the survey; and a survey without retention whose document can't be read. Upkeep used to
        // read every survey each run, so that one document stopped all of it.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));
        var kept = await harness.PublishAsync(definition, item => item.Retention = new SurveyRetentionSettings { Days = 30, Action = RetentionAction.Anonymize });
        var legacy = await harness.PublishAsync(definition, item => item.Retention = new SurveyRetentionSettings { Days = 30, Action = RetentionAction.Anonymize });
        var unreadable = await harness.PublishAsync(definition);
        var first = (await harness.StartAsync(kept, "u1")).Response;
        var second = (await harness.StartAsync(legacy, "u2")).Response;

        await harness.PostAsync(first, new SurveyRespondentIdentity { UserId = "u1" }, SurveyPageDirection.Finish, ("q1", "a"));
        await harness.PostAsync(second, new SurveyRespondentIdentity { UserId = "u2" }, SurveyPageDirection.Finish, ("q1", "b"));

        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var configuration = session.Store.Configuration;
            var dialect = configuration.SqlDialect;
            var documents = dialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetDocumentTable(SurveysConstants.CollectionName), configuration.Schema);
            var index = dialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetIndexTable(typeof(SurveyIndex), SurveysConstants.CollectionName), configuration.Schema);
            var connection = await session.CreateConnectionAsync();

            Assert.Equal(1, await connection.ExecuteAsync(
                $"UPDATE {index} SET {dialect.QuoteForColumnName("RetentionDays")} = NULL WHERE {dialect.QuoteForColumnName("ItemId")} = @Id",
                new { Id = legacy.ItemId }));
            Assert.Equal(1, await connection.ExecuteAsync(
                $"UPDATE {documents} SET {dialect.QuoteForColumnName("Content")} = @Content WHERE {dialect.QuoteForColumnName("Content")} LIKE @Match",
                new { Content = "{ not json", Match = $"%\"ItemId\":\"{unreadable.ItemId}\"%" }));

            return true;
        });

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddDays(40);
        await RunMaintenanceAsync(harness);

        // Assert
        Assert.Null((await harness.FindAsync(first.ItemId)).UserId);
        Assert.Null((await harness.FindAsync(second.ItemId)).UserId);
    }

    [Fact]
    public async Task SurveyUpkeepReindexStep_FillsTheUpkeepColumnsOfSurveysIndexedBefore()
    {
        // Arrange: a survey whose index row predates the upkeep columns.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))), item =>
        {
            item.Retention = new SurveyRetentionSettings { Days = 90, Action = RetentionAction.Delete };
            item.Access.Limits.Total = 2;
        });

        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var configuration = session.Store.Configuration;
            var dialect = configuration.SqlDialect;
            var index = dialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetIndexTable(typeof(SurveyIndex), SurveysConstants.CollectionName), configuration.Schema);
            var connection = await session.CreateConnectionAsync();

            await connection.ExecuteAsync(
                $"UPDATE {index} SET {dialect.QuoteForColumnName("RetentionDays")} = NULL, {dialect.QuoteForColumnName("TotalLimit")} = NULL WHERE {dialect.QuoteForColumnName("ItemId")} = @Id",
                new { Id = survey.ItemId });

            return true;
        });

        // Act
        var done = await harness.RunAsync(async services =>
        {
            var step = ActivatorUtilities.CreateInstance<CrestApps.OrchardCore.Surveys.Migrations.SurveyUpkeepReindexStep>(services);
            var finished = await step.RunAsync(new CrestApps.OrchardCore.YesSql.Core.Upgrades.ResumableStepContext(), TestContext.Current.CancellationToken);
            await services.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

            return finished;
        });

        // Assert
        var row = await harness.RunAsync(async services => await services.GetRequiredService<ISession>()
            .QueryIndex<SurveyIndex>(index => index.ItemId == survey.ItemId, collection: SurveysConstants.CollectionName)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken));

        Assert.True(done);
        Assert.Equal(90, row.RetentionDays);
        Assert.Equal(2, row.TotalLimit);
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
    public async Task Run_MoreOverdueResponsesOfASurveyWhoseSecretCantBeReadThanABatch_DoNotKeepAnotherSurveysOverdueResponseWaiting()
    {
        // Arrange: survey A's overdue responses all fail because of the server (its secret can't be read here), more of
        // them than a batch, and they are first in line; survey B's overdue response comes after them (R5-L2).
        var surveyA = string.Empty;
        var failure = new SaveFailureIndexProvider<SurveyResponse>(SurveysConstants.ResponsesCollectionName, response =>
            response.SurveyId == surveyA && response.Status != SurveyResponseStatus.InProgress ? new SurveyAnonymitySecretUnreadableException() : null);
        await using var harness = await CreateWithSaveFailureAsync(failure);
        var a = await harness.PublishAsync(TimedDefinition());
        var b = await harness.PublishAsync(TimedDefinition());
        var failing = new List<string>();

        for (var index = 0; index < 7; index++)
        {
            failing.Add((await harness.StartAsync(a, "a" + index)).Response.ItemId);
        }

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddSeconds(30);
        var waiting = (await harness.StartAsync(b, "b1")).Response;

        surveyA = a.ItemId;
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddMinutes(10);

        // Act
        await RunMaintenanceAsync(harness, batchSize: 3);

        // Assert
        Assert.Equal(SurveyResponseStatus.Completed, (await harness.FindAsync(waiting.ItemId)).Status);

        foreach (var id in failing)
        {
            var response = await harness.FindAsync(id);

            Assert.Equal(SurveyResponseStatus.InProgress, response.Status);
            Assert.Equal(0, response.UpkeepFailures);
        }
    }

    [Fact]
    public async Task Run_AFailureWhoseRowALaterPageReadsAgain_IsCountedOnceInTheRun()
    {
        // Arrange: two overdue responses with the same deadline, a batch of two. The first one's submit fails, and counting
        // the failure writes its index row again under a larger id, behind the second; the second one's survey fails
        // because of the server, so the walk reads another page, which used to hold the first response again (R6-L7).
        var poisonedId = string.Empty;
        var surveyB = string.Empty;
        var failure = new SaveFailureIndexProvider<SurveyResponse>(SurveysConstants.ResponsesCollectionName, response =>
            response.Status == SurveyResponseStatus.InProgress ? null
            : response.ItemId == poisonedId ? new InvalidOperationException("index provider failed")
            : response.SurveyId == surveyB ? new SurveyAnonymitySecretUnreadableException()
            : null);
        await using var harness = await CreateWithSaveFailureAsync(failure);
        var a = await harness.PublishAsync(TimedDefinition());
        var b = await harness.PublishAsync(TimedDefinition());
        var poisoned = (await harness.StartAsync(a, "u1")).Response;
        var environmental = (await harness.StartAsync(b, "u2")).Response;

        poisonedId = poisoned.ItemId;
        surveyB = b.ItemId;
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddMinutes(10);

        // Act
        await RunMaintenanceAsync(harness, batchSize: 2);

        // Assert
        var failed = await harness.FindAsync(poisoned.ItemId);

        Assert.Equal(poisoned.DeadlineUtc, environmental.DeadlineUtc);
        Assert.Equal(SurveyResponseStatus.InProgress, failed.Status);
        Assert.Equal(1, failed.UpkeepFailures);
        Assert.Equal(0, (await harness.FindAsync(environmental.ItemId)).UpkeepFailures);
    }

    [Fact]
    public async Task Run_AResponseWhoseFailureCantBeCounted_DoesNotBlockTheBatch()
    {
        // Arrange: the first overdue response fails, and so does the write of its failure count; a batch of one (R5-L2).
        var poisonedId = string.Empty;
        var failure = new SaveFailureIndexProvider<SurveyResponse>(SurveysConstants.ResponsesCollectionName, response =>
            response.ItemId == poisonedId ? new InvalidOperationException("index provider failed") : null);
        await using var harness = await CreateWithSaveFailureAsync(failure);
        var survey = await harness.PublishAsync(TimedDefinition());
        var poisoned = (await harness.StartAsync(survey, "u1")).Response;

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddSeconds(30);
        var healthy = (await harness.StartAsync(survey, "u2")).Response;

        poisonedId = poisoned.ItemId;
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddMinutes(10);

        // Act
        await RunMaintenanceAsync(harness, batchSize: 1);

        // Assert
        var failed = await harness.FindAsync(poisoned.ItemId);

        Assert.Equal(SurveyResponseStatus.Completed, (await harness.FindAsync(healthy.ItemId)).Status);
        Assert.Equal(SurveyResponseStatus.InProgress, failed.Status);
        Assert.Equal(0, failed.UpkeepFailures);
    }

    [Fact]
    public async Task Run_ResponsesOfASurveyWithALongExpiry_DoNotKeepAnotherSurveysIdleResponseFromBeingAbandoned()
    {
        // Arrange: the oldest in-progress responses belong to a survey whose responses expire after 30 days, so they
        // aren't idle yet; another survey's response, a minute newer, is idle after the default week.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));
        var patient = await harness.PublishAsync(definition, item => item.Access = new SurveyAccessSettings { IncompleteExpiryHours = 720 });
        var other = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var notIdle = new List<string>();

        for (var index = 0; index < 5; index++)
        {
            notIdle.Add((await harness.StartAsync(patient, "p" + index)).Response.ItemId);
        }

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddMinutes(1);
        var idle = (await harness.StartAsync(other, "o1")).Response;

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddDays(10);

        // Act
        await RunMaintenanceAsync(harness, batchSize: 2);

        // Assert
        Assert.Equal(SurveyResponseStatus.Abandoned, (await harness.FindAsync(idle.ItemId)).Status);

        foreach (var id in notIdle)
        {
            Assert.Equal(SurveyResponseStatus.InProgress, (await harness.FindAsync(id)).Status);
        }
    }

    [Fact]
    public async Task Run_AnOldParticipationRecordThatCantBeRead_DoesNotStopTheCleanup()
    {
        // Arrange: 7 old records, one of them unreadable and first in line, and a batch of 3.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var old = DateOnly.FromDateTime(RuntimeTestHarness.Now).AddDays(-SurveyParticipationService.RetentionDays - 5);

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyParticipationStore>();

            for (var index = 0; index < 7; index++)
            {
                await store.CreateAsync(new SurveyParticipation { SurveyId = "s1", RespondentKey = "k" + index, Day = old, StartedCount = 1 });
            }

            return true;
        });
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var configuration = session.Store.Configuration;
            var dialect = configuration.SqlDialect;
            var documents = dialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetDocumentTable(SurveysConstants.CollectionName), configuration.Schema);
            var connection = await session.CreateConnectionAsync();
            var changed = await connection.ExecuteAsync(
                $"UPDATE {documents} SET {dialect.QuoteForColumnName("Content")} = @Content WHERE {dialect.QuoteForColumnName("Content")} LIKE @Match",
                new { Content = "{ not json", Match = "%\"k0\"%" });

            Assert.Equal(1, changed);

            return true;
        });

        // Act
        await RunMaintenanceAsync(harness, batchSize: 3);

        // Assert: only the unreadable record is left, to be tried again on the next run.
        var left = await harness.RunAsync(async services => (await services.GetRequiredService<ISession>()
            .QueryIndex<SurveyParticipationIndex>(collection: SurveysConstants.CollectionName)
            .ListAsync(TestContext.Current.CancellationToken))
            .Select(row => row.RespondentKey)
            .ToArray());

        Assert.Equal(["k0"], left);
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

    [Fact]
    public void Anonymize_ClearsTheSiteWideResponseNumber()
    {
        // Arrange: R7-Mi15, the number comes from one sequence for the whole site, in the order responses start.
        var response = new SurveyResponse { Number = "R0000042" };

        // Act and assert
        Assert.True(SurveyMaintenanceService.Anonymize(response));
        Assert.Null(response.Number);
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

    private static SurveyDefinition TimedDefinition()
    {
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));

        definition.Timing.TimeLimitMinutes = 1;

        return definition;
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

using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Migrations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.Authoring;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Dapper;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// The upkeep, versions and invitation reminder reindex steps save what they load with YesSql's concurrency check, as
/// the survey reindex step does: a save that lands while a run holds its page (an author's edit, a delete mark, a send)
/// is never overwritten with the copy the run loaded; the run's commit fails, and the next run reads the page again
/// (R6-L24).
/// </summary>
public sealed class SurveyReindexStepsConcurrencyTests
{
    [Fact]
    public async Task SurveyUpkeepReindexStep_NeverOverwritesASurveySavedMeanwhile_AndTheNextRunIndexesIt()
    {
        // Arrange: a survey whose index row predates the upkeep columns.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))), item => item.Retention = new SurveyRetentionSettings { Days = 90, Action = RetentionAction.Delete });
        var store = await StoreAsync(harness);

        await ClearColumnAsync(store, typeof(SurveyIndex), "RetentionDays", survey.ItemId);

        // Act and assert: a run whose survey another save changed after it loaded it.
        await AssertTheRunsCommitFailsAsync(store, session => new SurveyUpkeepReindexStep(session, new SurveyStore(session)).RunAsync(new ResumableStepContext(), TestContext.Current.CancellationToken));

        // The next run, with nothing changed meanwhile, indexes it.
        await using (var run = store.CreateSession())
        {
            await new SurveyUpkeepReindexStep(run, new SurveyStore(run)).RunAsync(new ResumableStepContext(), TestContext.Current.CancellationToken);
            await run.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = store.CreateSession();
        var row = await read.QueryIndex<SurveyIndex>(index => index.ItemId == survey.ItemId, collection: SurveysConstants.CollectionName).FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.Equal(90, row.RetentionDays);
    }

    [Fact]
    public async Task SurveyVersionsReindexStep_NeverOverwritesAVersionSavedMeanwhile()
    {
        // Arrange: a published version whose index row predates the publisher column.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var store = await StoreAsync(harness);

        await ClearColumnAsync(store, typeof(SurveyVersionIndex), "PublishedBy", itemId: null, surveyId: survey.ItemId);

        // Act and assert
        await AssertTheRunsCommitFailsAsync(store, session => new SurveyVersionsReindexStep(session, new SurveyVersionStore(session)).RunAsync(new ResumableStepContext(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SurveyInvitationRemindersStep_NeverOverwritesAnInvitationSavedMeanwhile()
    {
        // Arrange: a reminded invitation whose index row predates the reminder count.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var invitationId = IdGenerator.GenerateId();

        await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<SurveyInvitationStore>().CreateAsync(new SurveyInvitation
            {
                ItemId = invitationId,
                SurveyId = "s1",
                CollectorId = "c1",
                Email = "reminded@example.com",
                Status = SurveyInvitationStatus.Sent,
                SentUtc = RuntimeTestHarness.Now.AddDays(-3),
                RemindersSent = 1,
                LastReminderUtc = RuntimeTestHarness.Now.AddDays(-2),
            });

            return true;
        });

        var store = await StoreAsync(harness);

        await using (var session = store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var dialect = store.Configuration.SqlDialect;

            await transaction.Connection.ExecuteAsync(
                $"UPDATE {IndexTable(store, typeof(SurveyInvitationIndex))} SET {dialect.QuoteForColumnName("RemindersSent")} = 0 WHERE {dialect.QuoteForColumnName("ItemId")} = @Id",
                new { Id = invitationId },
                transaction);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        // Act and assert
        await AssertTheRunsCommitFailsAsync(store, session => new SurveyInvitationRemindersStep(session, new SurveyInvitationStore(session)).RunAsync(new ResumableStepContext(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ContentSanitizingStep_TemplateWalk_NeverOverwritesATemplateSavedMeanwhile()
    {
        // Arrange (R7-Mi12): a template stored before the stores sanitized on save.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var store = await StoreAsync(harness);
        store.RegisterIndexes(new SurveyTemplateIndexProvider());
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));
        definition.Pages[0].Questions[0].Title = Dirty;

        await using (var session = store.CreateSession())
        {
            await new SurveyTemplateStore(session).CreateAsync(new SurveyTemplate { Name = "dirty", Title = "Dirty", Definition = definition }, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act and assert: the template walk is the step's third.
        await AssertTheRunsCommitFailsAsync(store, session => SanitizingStep(session).RunAsync(new ResumableStepContext { Cursor = "2|" }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ContentSanitizingStep_BankQuestionWalk_NeverOverwritesAQuestionSavedMeanwhile()
    {
        // Arrange (R7-Mi12)
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var store = await StoreAsync(harness);

        await using (var session = store.CreateSession())
        {
            await new BankQuestionStore(session).CreateAsync(new BankQuestion { BankId = "b1", Question = new SurveyQuestion { Id = "q1", Name = "q1", ControlType = "text", Title = Dirty } }, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act and assert: the bank question walk is the step's fourth.
        await AssertTheRunsCommitFailsAsync(store, session => SanitizingStep(session).RunAsync(new ResumableStepContext { Cursor = "3|" }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnonymityUpgradeStep_ParticipationWalk_NeverOverwritesACompletionCountedMeanwhile()
    {
        // Arrange (R7-Mi12): an anonymous participation record with an exact completion time, written by an earlier
        // version; a completion counts on it while the walk holds its page.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var store = await StoreAsync(harness);
        var completed = RuntimeTestHarness.Now.AddHours(-3);

        await using (var session = store.CreateSession())
        {
            await session.SaveAsync(new SurveyParticipation { ItemId = "anonymous", SurveyId = "s1", RespondentKey = "a:hmac", Day = DateOnly.FromDateTime(completed), CompletedCount = 1, LastCompletedUtc = completed }, false, SurveysConstants.CollectionName, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act and assert: the participation walk is the step's second.
        await AssertTheRunsCommitFailsAsync(store, session => new SurveyAnonymityUpgradeStep(
            session,
            new SurveyStore(session),
            new SurveyParticipationStore(session),
            new SurveyAnonymitySecrets(new EphemeralDataProtectionProvider(), new SurveyStore(session), NullLogger<SurveyAnonymitySecrets>.Instance))
            .RunAsync(new ResumableStepContext { Cursor = "1|" }, TestContext.Current.CancellationToken));
    }

    private const string Dirty = "<p>Hello</p><script>alert('XSS')</script>";

    private static SurveyContentSanitizingStep SanitizingStep(ISession session)
        => new(session, new SurveyStore(session), new SurveyVersionStore(session), new SurveyTemplateStore(session), new BankQuestionStore(session), SurveyContentSanitizerTests.Sanitizer());

    // Runs a step's page, then moves on the stored version of every document, as another save would after the run loaded
    // them: the run's commit must fail rather than write its copies over them.
    private static async Task AssertTheRunsCommitFailsAsync(IStore store, Func<ISession, Task<bool>> runAsync)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var racing = store.CreateSession();

        await runAsync(racing);

        var transaction = await racing.BeginTransactionAsync(cancellationToken);
        var configuration = store.Configuration;
        var dialect = configuration.SqlDialect;
        var documents = dialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetDocumentTable(SurveysConstants.CollectionName), configuration.Schema);
        var version = dialect.QuoteForColumnName("Version");

        await transaction.Connection.ExecuteAsync($"UPDATE {documents} SET {version} = {version} + 1", transaction: transaction);

        await Assert.ThrowsAsync<ConcurrencyException>(() => racing.SaveChangesAsync(cancellationToken));
    }

    private static Task<IStore> StoreAsync(RuntimeTestHarness harness)
        => harness.RunAsync(services => Task.FromResult(services.GetRequiredService<IStore>()));

    private static async Task ClearColumnAsync(IStore store, Type index, string column, string itemId, string surveyId = null)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var dialect = store.Configuration.SqlDialect;
        var (key, value) = itemId is null ? ("SurveyId", surveyId) : ("ItemId", itemId);

        Assert.True(await transaction.Connection.ExecuteAsync(
            $"UPDATE {IndexTable(store, index)} SET {dialect.QuoteForColumnName(column)} = NULL WHERE {dialect.QuoteForColumnName(key)} = @Value",
            new { Value = value },
            transaction) > 0);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static string IndexTable(IStore store, Type index)
    {
        var configuration = store.Configuration;

        return configuration.SqlDialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetIndexTable(index, SurveysConstants.CollectionName), configuration.Schema);
    }
}

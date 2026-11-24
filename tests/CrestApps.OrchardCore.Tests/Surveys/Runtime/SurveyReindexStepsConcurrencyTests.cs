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
/// The resumable steps' walks never overwrite a save that lands while a run holds its page (R8-M1). The competing save
/// is a real save through the store, from a session of its own, committed after the walk loaded the page and before it
/// writes (<see cref="RacingSession"/>): normal saves don't move YesSql's document version, so the walks compare the
/// record's index row id instead (<see cref="SurveyUpgradePages.SaveUnchangedAsync"/>).
/// </summary>
public sealed class SurveyReindexStepsConcurrencyTests
{
    private const string Dirty = "<p>Hello</p><script>alert('XSS')</script>";

    private const string Edited = "Edited meanwhile";

    [Fact]
    public async Task SurveyUpkeepReindexStep_KeepsASurveySavedMeanwhile_AndIndexesTheOthers()
    {
        // Arrange: two surveys whose index rows predate the upkeep columns.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var edited = await harness.PublishAsync(Definition(), item => item.Retention = new SurveyRetentionSettings { Days = 90, Action = RetentionAction.Delete });
        var other = await harness.PublishAsync(Definition(), item => item.Retention = new SurveyRetentionSettings { Days = 30, Action = RetentionAction.Delete });
        var store = await StoreAsync(harness);

        await ClearColumnAsync(store, typeof(SurveyIndex), "RetentionDays", edited.ItemId);
        await ClearColumnAsync(store, typeof(SurveyIndex), "RetentionDays", other.ItemId);

        // Act: an author saves the first survey after the run loaded the page.
        await RunRacingAsync(
            store,
            session => new SurveyUpkeepReindexStep(session, new SurveyStore(session)).RunAsync(new ResumableStepContext(), TestContext.Current.CancellationToken),
            session => EditSurveyAsync(session, edited.ItemId));

        // Assert: the edit is kept (its own save filled the column), and the run indexed the other survey.
        Assert.Equal(Edited, (await LoadAsync<Survey>(store, edited.ItemId)).Title);
        Assert.Equal(90, (await RowAsync<SurveyIndex>(store, edited.ItemId)).RetentionDays);
        Assert.Equal(30, (await RowAsync<SurveyIndex>(store, other.ItemId)).RetentionDays);
    }

    [Fact]
    public async Task SurveysReindexStep_KeepsASurveySavedMeanwhile()
    {
        // Arrange: a survey whose index row predates the anonymity column.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(Definition());
        var store = await StoreAsync(harness);

        await ClearColumnAsync(store, typeof(SurveyIndex), "Anonymity", survey.ItemId);

        // Act
        await RunRacingAsync(
            store,
            session => new SurveysReindexStep(session, new SurveyStore(session)).RunAsync(new ResumableStepContext(), TestContext.Current.CancellationToken),
            session => EditSurveyAsync(session, survey.ItemId));

        // Assert
        Assert.Equal(Edited, (await LoadAsync<Survey>(store, survey.ItemId)).Title);
        Assert.NotNull((await RowAsync<SurveyIndex>(store, survey.ItemId)).Anonymity);
    }

    [Fact]
    public async Task SurveyVersionsReindexStep_KeepsAVersionSavedMeanwhile()
    {
        // Arrange: a published version whose index row predates the publisher column.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(Definition());
        var store = await StoreAsync(harness);

        await ClearColumnAsync(store, typeof(SurveyVersionIndex), "PublishedBy", itemId: null, surveyId: survey.ItemId);

        // Act: someone edits the version's notes after the run loaded it.
        await RunRacingAsync(
            store,
            session => new SurveyVersionsReindexStep(session, new SurveyVersionStore(session)).RunAsync(new ResumableStepContext(), TestContext.Current.CancellationToken),
            async session =>
            {
                var versions = new SurveyVersionStore(session);
                var version = await versions.FindByIdAsync(survey.PublishedVersionId, TestContext.Current.CancellationToken);

                version.Notes = Edited;
                await versions.UpdateAsync(version, TestContext.Current.CancellationToken);
            });

        // Assert
        Assert.Equal(Edited, (await LoadAsync<SurveyVersion>(store, survey.PublishedVersionId)).Notes);
    }

    [Fact]
    public async Task ContentSanitizingStep_SurveyWalk_KeepsADraftSavedMeanwhile()
    {
        // Arrange (R7-Mi12): a survey whose draft was stored before the stores sanitized on save.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(Definition());
        var store = await StoreAsync(harness);

        await StoreRawAsync(store, survey.ItemId, (Survey item) => item.Draft.Pages[0].Questions[0].Title = Dirty);

        // Act: the survey walk is the step's first.
        await RunRacingAsync(
            store,
            session => SanitizingStep(session).RunAsync(new ResumableStepContext(), TestContext.Current.CancellationToken),
            session => EditSurveyAsync(session, survey.ItemId));

        // Assert: the author's save is kept, and it sanitized the draft itself.
        var saved = await LoadAsync<Survey>(store, survey.ItemId);

        Assert.Equal(Edited, saved.Title);
        SurveyContentSanitizerTests.AssertSafe(saved.Draft.Pages[0].Questions[0].Title);
    }

    [Fact]
    public async Task ContentSanitizingStep_VersionWalk_KeepsAVersionSavedMeanwhile()
    {
        // Arrange (R7-Mi12)
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(Definition());
        var store = await StoreAsync(harness);

        await StoreRawAsync(store, survey.PublishedVersionId, (SurveyVersion item) => item.Definition.Pages[0].Questions[0].Title = Dirty);

        // Act: the version walk is the step's second.
        await RunRacingAsync(
            store,
            session => SanitizingStep(session).RunAsync(new ResumableStepContext { Cursor = "1|" }, TestContext.Current.CancellationToken),
            async session =>
            {
                var versions = new SurveyVersionStore(session, SurveyContentSanitizerTests.Sanitizer());
                var version = await versions.FindByIdAsync(survey.PublishedVersionId, TestContext.Current.CancellationToken);

                version.Notes = Edited;
                await versions.UpdateAsync(version, TestContext.Current.CancellationToken);
            });

        // Assert
        var saved = await LoadAsync<SurveyVersion>(store, survey.PublishedVersionId);

        Assert.Equal(Edited, saved.Notes);
        SurveyContentSanitizerTests.AssertSafe(saved.Definition.Pages[0].Questions[0].Title);
    }

    [Fact]
    public async Task ContentSanitizingStep_TemplateWalk_KeepsATemplateSavedMeanwhile()
    {
        // Arrange (R7-Mi12): a template stored before the stores sanitized on save.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var store = await StoreAsync(harness);
        store.RegisterIndexes(new SurveyTemplateIndexProvider());
        var definition = Definition();
        definition.Pages[0].Questions[0].Title = Dirty;
        var template = new SurveyTemplate { Name = "dirty", Title = "Dirty", Definition = definition };

        await using (var session = store.CreateSession())
        {
            await new SurveyTemplateStore(session).CreateAsync(template, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act: the template walk is the step's third.
        await RunRacingAsync(
            store,
            session => SanitizingStep(session).RunAsync(new ResumableStepContext { Cursor = "2|" }, TestContext.Current.CancellationToken),
            async session =>
            {
                var templates = new SurveyTemplateStore(session, SurveyContentSanitizerTests.Sanitizer());
                var loaded = await templates.FindByIdAsync(template.ItemId, TestContext.Current.CancellationToken);

                loaded.Title = Edited;
                await templates.UpdateAsync(loaded, TestContext.Current.CancellationToken);
            });

        // Assert
        var saved = await LoadAsync<SurveyTemplate>(store, template.ItemId);

        Assert.Equal(Edited, saved.Title);
        SurveyContentSanitizerTests.AssertSafe(saved.Definition.Pages[0].Questions[0].Title);
    }

    [Fact]
    public async Task ContentSanitizingStep_BankQuestionWalk_KeepsAQuestionSavedMeanwhile()
    {
        // Arrange (R7-Mi12)
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var store = await StoreAsync(harness);
        var question = new BankQuestion { BankId = "b1", Question = new SurveyQuestion { Id = "q1", Name = "q1", ControlType = "text", Title = Dirty } };

        await using (var session = store.CreateSession())
        {
            await new BankQuestionStore(session).CreateAsync(question, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act: the bank question walk is the step's fourth.
        await RunRacingAsync(
            store,
            session => SanitizingStep(session).RunAsync(new ResumableStepContext { Cursor = "3|" }, TestContext.Current.CancellationToken),
            async session =>
            {
                var questions = new BankQuestionStore(session, SurveyContentSanitizerTests.Sanitizer());
                var loaded = await questions.FindByIdAsync(question.ItemId, TestContext.Current.CancellationToken);

                loaded.Difficulty = 5;
                await questions.UpdateAsync(loaded, TestContext.Current.CancellationToken);
            });

        // Assert
        var saved = await LoadAsync<BankQuestion>(store, question.ItemId);

        Assert.Equal(5, saved.Difficulty);
        SurveyContentSanitizerTests.AssertSafe(saved.Question.Title);
    }

    [Fact]
    public async Task AnonymityUpgradeStep_SurveyWalk_KeepsASurveySavedMeanwhile()
    {
        // Arrange: a survey whose secret an earlier version stored in plain text.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(Definition());
        var store = await StoreAsync(harness);

        await StoreRawAsync(store, survey.ItemId, (Survey item) => item.AnonymitySecret = "plain");

        // Act: the survey walk is the step's first; the author's save goes through the store, which protects the secret.
        await RunRacingAsync(
            store,
            session => AnonymityStep(session).RunAsync(new ResumableStepContext(), TestContext.Current.CancellationToken),
            session => EditSurveyAsync(session, survey.ItemId, protect: true));

        // Assert
        var saved = await LoadAsync<Survey>(store, survey.ItemId);

        Assert.Equal(Edited, saved.Title);
        Assert.NotEqual("plain", saved.AnonymitySecret);
    }

    [Fact]
    public async Task AnonymityUpgradeStep_ParticipationWalk_NeverWritesBackARecordPrunedMeanwhile()
    {
        // Arrange (R7-Mi12): an anonymous participation record with an exact completion time, written by an earlier
        // version. Participation records are never updated once written; upkeep prunes them while the walk holds its page.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var store = await StoreAsync(harness);
        var completed = RuntimeTestHarness.Now.AddHours(-3);
        var record = new SurveyParticipation { ItemId = IdGenerator.GenerateId(), SurveyId = "s1", RespondentKey = "a:hmac", Day = DateOnly.FromDateTime(completed), CompletedCount = 1, LastCompletedUtc = completed };

        await using (var session = store.CreateSession())
        {
            await session.SaveAsync(record, false, SurveysConstants.CollectionName, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act: the participation walk is the step's second.
        await RunRacingAsync(
            store,
            session => AnonymityStep(session).RunAsync(new ResumableStepContext { Cursor = "1|" }, TestContext.Current.CancellationToken),
            async session =>
            {
                var participation = new SurveyParticipationStore(session);

                await participation.DeleteAsync(await participation.FindByIdAsync(record.ItemId, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
            });

        // Assert: the record stays deleted, with no index row written back for it.
        Assert.Null(await RowAsync<SurveyParticipationIndex>(store, record.ItemId));
        Assert.Null(await LoadAsync<SurveyParticipation>(store, record.ItemId));
    }

    [Fact]
    public async Task SurveyInvitationRemindersStep_KeepsACompletionSavedMeanwhile()
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

        await SetColumnAsync(store, typeof(SurveyInvitationIndex), "RemindersSent", 0, invitationId);

        // Act: the respondent completes the invitation after the run loaded it.
        await RunRacingAsync(
            store,
            session => new SurveyInvitationRemindersStep(session, new SurveyInvitationStore(session)).RunAsync(new ResumableStepContext(), TestContext.Current.CancellationToken),
            async session =>
            {
                var invitations = new SurveyInvitationStore(session);
                var invitation = await invitations.FindByIdAsync(invitationId, TestContext.Current.CancellationToken);

                invitation.Status = SurveyInvitationStatus.Completed;
                await invitations.UpdateAsync(invitation, TestContext.Current.CancellationToken);
            });

        // Assert
        Assert.Equal(SurveyInvitationStatus.Completed, (await LoadAsync<SurveyInvitation>(store, invitationId)).Status);
        Assert.Equal(nameof(SurveyInvitationStatus.Completed), (await RowAsync<SurveyInvitationIndex>(store, invitationId)).Status);
    }

    private static SurveyDefinition Definition()
        => RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));

    private static SurveyContentSanitizingStep SanitizingStep(ISession session)
        => new(session, new SurveyStore(session), new SurveyVersionStore(session), new SurveyTemplateStore(session), new BankQuestionStore(session), SurveyContentSanitizerTests.Sanitizer());

    private static SurveyAnonymityUpgradeStep AnonymityStep(ISession session)
        => new(
            session,
            new SurveyStore(session),
            new SurveyParticipationStore(session),
            new SurveyAnonymitySecrets(new EphemeralDataProtectionProvider(), new SurveyStore(session), NullLogger<SurveyAnonymitySecrets>.Instance));

    // An author's save of a survey, through the store.
    private static async Task EditSurveyAsync(ISession session, string surveyId, bool protect = false)
    {
        var surveys = new SurveyStore(session, SurveyContentSanitizerTests.Sanitizer(), protect ? new EphemeralDataProtectionProvider() : null);
        var survey = await surveys.FindByIdAsync(surveyId, TestContext.Current.CancellationToken);

        survey.Title = Edited;
        await surveys.UpdateAsync(survey, TestContext.Current.CancellationToken);
    }

    // Runs a step's page on a session whose competing save is committed, from a session of its own, after the walk loaded
    // the page's records and before it checks and writes them; then commits the run, which must not fail.
    private static async Task RunRacingAsync(IStore store, Func<ISession, Task<bool>> runAsync, Func<ISession, Task> competingSave)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var raced = false;

        await using var walk = store.CreateSession();
        var racing = RacingSession.Create(walk, async () =>
        {
            raced = true;

            await using var other = store.CreateSession();

            await competingSave(other);
            await other.SaveChangesAsync(cancellationToken);
        });

        await runAsync(racing);
        await walk.SaveChangesAsync(cancellationToken);

        Assert.True(raced, "The competing save never ran: the walk did not load and then check its page.");
    }

    private static Task<IStore> StoreAsync(RuntimeTestHarness harness)
        => harness.RunAsync(services => Task.FromResult(services.GetRequiredService<IStore>()));

    // Stores a record past its store, as an earlier version saved it.
    private static async Task StoreRawAsync<T>(IStore store, string itemId, Action<T> change)
        where T : class
    {
        await using var session = store.CreateSession();
        var record = (await session.Query<T>(collection: SurveysConstants.CollectionName).ListAsync(TestContext.Current.CancellationToken))
            .Single(item => item is CrestApps.Core.Models.CatalogItem catalogItem && catalogItem.ItemId == itemId);

        change(record);
        await session.SaveAsync(record, false, SurveysConstants.CollectionName, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<T> LoadAsync<T>(IStore store, string itemId)
        where T : class
    {
        await using var session = store.CreateSession();

        return (await session.Query<T>(collection: SurveysConstants.CollectionName).ListAsync(TestContext.Current.CancellationToken))
            .SingleOrDefault(item => item is CrestApps.Core.Models.CatalogItem catalogItem && catalogItem.ItemId == itemId);
    }

    private static async Task<TIndex> RowAsync<TIndex>(IStore store, string itemId)
        where TIndex : CrestApps.Core.Data.YesSql.Indexes.CatalogItemIndex
    {
        await using var session = store.CreateSession();

        return await session.QueryIndex<TIndex>(index => index.ItemId == itemId, collection: SurveysConstants.CollectionName).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
    }

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

    private static async Task SetColumnAsync(IStore store, Type index, string column, object value, string itemId)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var dialect = store.Configuration.SqlDialect;

        Assert.True(await transaction.Connection.ExecuteAsync(
            $"UPDATE {IndexTable(store, index)} SET {dialect.QuoteForColumnName(column)} = @Value WHERE {dialect.QuoteForColumnName("ItemId")} = @Id",
            new { Value = value, Id = itemId },
            transaction) > 0);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static string IndexTable(IStore store, Type index)
    {
        var configuration = store.Configuration;

        return configuration.SqlDialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetIndexTable(index, SurveysConstants.CollectionName), configuration.Schema);
    }
}

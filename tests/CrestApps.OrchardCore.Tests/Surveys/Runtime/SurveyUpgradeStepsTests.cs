using CrestApps.Core;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Migrations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.Authoring;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Documents;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// The data upgrades of Surveys run as resumable steps in the background, a page per run, and never inside a migration:
/// each walk visits every record once across pages, the steps finish, and the migrations only ask for them.
/// </summary>
public sealed class SurveyUpgradeStepsTests
{
    private const string Dirty = "<p>Hello</p><script>alert('XSS-DRAFT')</script>";

    [Fact]
    public async Task VisitPageAsync_SavingEveryRecord_VisitsEachOnceAcrossPagesAndRuns()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();

        for (var index = 0; index < 5; index++)
        {
            await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        }

        var visits = new List<string>();
        string cursor = null;
        var pages = 0;

        // Act: pages of 2, each in its own scope as the background task runs them, and every record changed and saved.
        do
        {
            cursor = await harness.RunAsync(services => SurveyUpgradePages.VisitPageAsync(
                services.GetRequiredService<ISession>(),
                services.GetRequiredService<SurveyStore>(),
                SurveysConstants.CollectionName,
                cursor,
                survey =>
                {
                    visits.Add(survey.ItemId);
                    survey.Title = "Visited";

                    return true;
                },
                pageSize: 2,
                cancellationToken: TestContext.Current.CancellationToken));
            pages++;
        }
        while (cursor is not null && pages < 10);

        // Assert
        var surveys = await harness.RunAsync(async services => await services.GetRequiredService<SurveyStore>().GetAllAsync());

        Assert.Null(cursor);
        Assert.Equal(3, pages);
        Assert.Equal(5, visits.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(5, visits.Count);
        Assert.All(surveys, survey => Assert.Equal("Visited", survey.Title));
    }

    [Fact]
    public async Task RunAsync_WalksEachPhaseInTurn_KeepingItsCursor_AndIsDoneAfterTheLast()
    {
        // Arrange: a first walk of two pages and a second walk of one.
        var seen = new List<string>();
        var context = new ResumableStepContext();
        Func<string, Task<string>>[] walks =
        [
            cursor =>
            {
                seen.Add("a:" + (cursor ?? "start"));

                return Task.FromResult(cursor is null ? "next" : null);
            },
            cursor =>
            {
                seen.Add("b:" + (cursor ?? "start"));

                return Task.FromResult<string>(null);
            },
        ];

        // Act
        var first = await SurveyUpgradePages.RunAsync(context, walks);
        var second = await SurveyUpgradePages.RunAsync(context, walks);
        var third = await SurveyUpgradePages.RunAsync(context, walks);

        // Assert
        Assert.False(first);
        Assert.False(second);
        Assert.True(third);
        Assert.Equal(["a:start", "a:next", "b:start"], seen);
    }

    [Fact]
    public async Task InvitationsReindexStep_FillsTheIndexColumnsOfInvitationsIndexedBefore()
    {
        // Arrange: an invitation whose e-mail column is empty, as on a site indexed before the column existed.
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddScoped<SurveyInvitationsReindexStep>());
        await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<SurveyInvitationStore>().CreateAsync(new SurveyInvitation { SurveyId = "s1", CollectorId = "c1", Email = "pat@example.com" });

            return true;
        });
        await harness.RunAsync(services => ClearColumnAsync<SurveyInvitationIndex>(services, SurveysConstants.CollectionName, "Email"));

        var before = await CountByEmailAsync(harness, "pat@example.com");

        // Act
        var runs = await RunUntilDoneAsync(harness, services => services.GetRequiredService<SurveyInvitationsReindexStep>());

        // Assert
        Assert.Equal(0, before);
        Assert.Equal(1, runs);
        Assert.Equal(1, await CountByEmailAsync(harness, "pat@example.com"));
    }

    [Fact]
    public async Task InvitationHoldsStep_MarksInvitationsHeldBeforeTheHoldColumn_InItsFirstRun_AndSavesOnlyThose()
    {
        // Arrange: a held first send, a pending one never sent and a sent one, indexed before the hold column existed.
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddScoped<SurveyInvitationHoldsStep>());
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyInvitationStore>();
            var held = new SurveyInvitation { ItemId = "held", SurveyId = "s1", CollectorId = "c1", Email = "held@example.com", Status = SurveyInvitationStatus.Pending };

            held.Put(new SurveyInvitationHold { HeldUtc = RuntimeTestHarness.Now });
            await store.CreateAsync(held);
            await store.CreateAsync(new SurveyInvitation { ItemId = "created", SurveyId = "s1", CollectorId = "c1", Email = "created@example.com", Status = SurveyInvitationStatus.Pending });
            await store.CreateAsync(new SurveyInvitation { ItemId = "sent", SurveyId = "s1", CollectorId = "c1", Email = "sent@example.com", Status = SurveyInvitationStatus.Sent });

            return true;
        });
        await harness.RunAsync(services => ResetHeldColumnAsync(services));

        var heldBefore = await ListHeldAsync(harness);
        var rowsBefore = await IndexRowIdsAsync(harness);

        // Act
        var runs = await RunUntilDoneAsync(harness, services => services.GetRequiredService<SurveyInvitationHoldsStep>());

        // Assert: the held invitation is found by the reminder task's query after one run; the others weren't saved.
        var rowsAfter = await IndexRowIdsAsync(harness);

        Assert.Empty(heldBefore);
        Assert.Equal(1, runs);
        Assert.Equal(["held"], await ListHeldAsync(harness));
        Assert.Equal(rowsBefore["created"], rowsAfter["created"]);
        Assert.Equal(rowsBefore["sent"], rowsAfter["sent"]);
    }

    [Fact]
    public async Task ContentSanitizingStep_CleansStoredDraftsAndVersions_AndLeavesCleanRecordsUnsaved()
    {
        // Arrange: a survey whose draft and published version were stored before the stores sanitized on save (the harness
        // registers no sanitizer, so its stores keep what they are given), and a clean one.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));

        definition.Pages[0].Questions[0].Title = Dirty;

        var dirty = await harness.PublishAsync(definition);
        var clean = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var cleanModified = (await harness.RunAsync(async services => await services.GetRequiredService<SurveyStore>().FindByIdAsync(clean.ItemId))).ModifiedUtc;

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddHours(1);

        // Act
        var runs = await RunUntilDoneAsync(harness, services => ActivatorUtilities.CreateInstance<SurveyContentSanitizingStep>(services, SurveyContentSanitizerTests.Sanitizer()));

        // Assert
        var (draft, version, untouched) = await harness.RunAsync(async services => (
            await services.GetRequiredService<SurveyStore>().FindByIdAsync(dirty.ItemId),
            await services.GetRequiredService<SurveyVersionStore>().FindByIdAsync(dirty.PublishedVersionId),
            await services.GetRequiredService<SurveyStore>().FindByIdAsync(clean.ItemId)));

        Assert.Equal(4, runs);
        SurveyContentSanitizerTests.AssertSafe(draft.Draft.Pages[0].Questions[0].Title);
        SurveyContentSanitizerTests.AssertSafe(version.Definition.Pages[0].Questions[0].Title);
        Assert.Equal(cleanModified, untouched.ModifiedUtc);
    }

    [Fact]
    public async Task AnonymityUpgradeStep_ProtectsPlainSecrets_AndClearsOnlyAnonymousCompletionTimes()
    {
        // Arrange: the harness stores the secret in plain text, as surveys saved before secrets were protected.
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddScoped<SurveyAnonymityUpgradeStep>());
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var completed = RuntimeTestHarness.Now.AddHours(-3);

        await harness.RunAsync(async services =>
        {
            var participation = services.GetRequiredService<SurveyParticipationStore>();

            await participation.CreateAsync(new SurveyParticipation { SurveyId = survey.ItemId, RespondentKey = "a:hmac", Day = DateOnly.FromDateTime(completed), CompletedCount = 1, LastCompletedUtc = completed });
            await participation.CreateAsync(new SurveyParticipation { SurveyId = survey.ItemId, RespondentKey = "u:pat", Day = DateOnly.FromDateTime(completed), CompletedCount = 1, LastCompletedUtc = completed });

            return true;
        });

        // Act
        var runs = await RunUntilDoneAsync(harness, services => services.GetRequiredService<SurveyAnonymityUpgradeStep>());

        // Assert
        var (stored, rows, secret) = await harness.RunAsync(async services =>
        {
            var upgraded = await services.GetRequiredService<SurveyStore>().FindByIdAsync(survey.ItemId);

            return (
                upgraded,
                await services.GetRequiredService<SurveyParticipationStore>().GetAllAsync(),
                await services.GetRequiredService<SurveyAnonymitySecrets>().RevealAsync(upgraded, TestContext.Current.CancellationToken));
        });

        Assert.Equal(2, runs);
        Assert.NotEqual("secret", stored.AnonymitySecret);
        Assert.Equal("secret", secret);
        Assert.Null(rows.Single(row => row.RespondentKey == "a:hmac").LastCompletedUtc);
        Assert.Equal(completed, rows.Single(row => row.RespondentKey == "u:pat").LastCompletedUtc);
    }

    [Fact]
    public async Task Migrations_AskForTheirStepsInsteadOfWalkingTheData()
    {
        // Arrange
        var document = new ResumableStepsDocument();
        var documents = new Mock<IDocumentManager<ResumableStepsDocument>>();
        documents.Setup(manager => manager.GetOrCreateMutableAsync(It.IsAny<Func<Task<ResumableStepsDocument>>>())).ReturnsAsync(document);
        var steps = new ResumableSteps(documents.Object);

        // Act
        var sanitizing = await new SurveyContentSanitizingMigrations(steps).CreateAsync();
        var anonymity = await new SurveyAnonymityMigrations(steps).CreateAsync();

        // Assert
        Assert.Equal(1, sanitizing);
        Assert.Equal(1, anonymity);
        Assert.Contains(SurveyContentSanitizingStep.StepName, document.Pending.Keys);
        Assert.Contains(SurveyAnonymityUpgradeStep.StepName, document.Pending.Keys);
    }

    private static async Task<int> RunUntilDoneAsync(RuntimeTestHarness harness, Func<IServiceProvider, IResumableStep> step)
    {
        var context = new ResumableStepContext();

        for (var run = 1; run <= 20; run++)
        {
            if (await harness.RunAsync(services => step(services).RunAsync(context, TestContext.Current.CancellationToken)))
            {
                return run;
            }
        }

        throw new InvalidOperationException("The step did not finish.");
    }

    private static async Task<bool> ClearColumnAsync<TIndex>(IServiceProvider services, string collection, string column)
    {
        var session = services.GetRequiredService<ISession>();
        var configuration = session.Store.Configuration;
        var dialect = configuration.SqlDialect;
        var table = dialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetIndexTable(typeof(TIndex), collection), configuration.Schema);
        var connection = await session.CreateConnectionAsync();

        await connection.ExecuteAsync($"UPDATE {table} SET {dialect.QuoteForColumnName(column)} = NULL");

        return true;
    }

    private static async Task<bool> ResetHeldColumnAsync(IServiceProvider services)
    {
        var session = services.GetRequiredService<ISession>();
        var configuration = session.Store.Configuration;
        var dialect = configuration.SqlDialect;
        var table = dialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetIndexTable(typeof(SurveyInvitationIndex), SurveysConstants.CollectionName), configuration.Schema);
        var connection = await session.CreateConnectionAsync();

        await connection.ExecuteAsync($"UPDATE {table} SET {dialect.QuoteForColumnName("Held")} = 0");

        return true;
    }

    private static Task<string[]> ListHeldAsync(RuntimeTestHarness harness)
        => harness.RunAsync(async services => (await services.GetRequiredService<SurveyInvitationStore>().ListHeldByCollectorAsync("c1", 10, TestContext.Current.CancellationToken))
            .Select(invitation => invitation.ItemId)
            .ToArray());

    private static Task<Dictionary<string, long>> IndexRowIdsAsync(RuntimeTestHarness harness)
        => harness.RunAsync(async services => (await services.GetRequiredService<ISession>()
            .QueryIndex<SurveyInvitationIndex>(collection: SurveysConstants.CollectionName)
            .ListAsync(TestContext.Current.CancellationToken))
            .ToDictionary(row => row.ItemId, row => row.Id, StringComparer.Ordinal));

    private static Task<int> CountByEmailAsync(RuntimeTestHarness harness, string email)
        => harness.RunAsync(services => services.GetRequiredService<ISession>()
            .QueryIndex<SurveyInvitationIndex>(index => index.Email == email, collection: SurveysConstants.CollectionName)
            .CountAsync(TestContext.Current.CancellationToken));
}

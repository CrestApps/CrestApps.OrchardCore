using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.Documents;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// The runtime Milds of the eighth review: the delete mark read by the index (R8-Mi5, R8-Mi33), a submit within the
/// closing grace that upkeep retries (R8-Mi8), and resumable steps that commit each in a scope of its own (R8-Mi7).
/// </summary>
public sealed class SurveyRuntimeRound8Tests
{
    [Fact]
    public async Task SurveyIndex_CarriesTheDeleteMark()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage());

        // Act: the mark is saved through the store, as the delete saves it.
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyStore>();
            var stored = await store.FindByIdAsync(survey.ItemId);

            stored.DeletingUtc = RuntimeTestHarness.Now;
            await store.UpdateAsync(stored);

            return true;
        });

        // Assert
        var row = await harness.RunAsync(services => services.GetRequiredService<ISession>()
            .QueryIndex<SurveyIndex>(index => index.ItemId == survey.ItemId, collection: SurveysConstants.CollectionName)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken));

        Assert.Equal(RuntimeTestHarness.Now, DateTime.SpecifyKind(row.DeletingUtc.Value, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Start_ReadsTheDeleteMarkByTheIndex_OnItsOwnSession()
    {
        // Arrange (R8-Mi5): only the survey's index row carries the mark, so a start that refuses read it there, not from
        // the survey document. The start's own session already loaded the survey, as a start does.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage());
        var store = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<IStore>()));

        await using (var session = store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var configuration = store.Configuration;
            var dialect = configuration.SqlDialect;
            var table = dialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetIndexTable(typeof(SurveyIndex), SurveysConstants.CollectionName), configuration.Schema);

            Assert.Equal(1, await transaction.Connection.ExecuteAsync(
                $"UPDATE {table} SET {dialect.QuoteForColumnName("DeletingUtc")} = @Now WHERE {dialect.QuoteForColumnName("ItemId")} = @Id",
                new { Now = RuntimeTestHarness.Now, Id = survey.ItemId },
                transaction));
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var refused = await harness.StartAsync(survey, "u1");

        // Assert
        Assert.Equal(SurveyErrorCode.NotFound, refused.Error);
        Assert.Null(refused.Response);
    }

    [Fact]
    public async Task Start_WithNoDeleteMark_WritesTheResponse()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(OnePage());

        // Act
        var started = await harness.StartAsync(survey, "u1");

        // Assert
        Assert.Equal(SurveyErrorCode.None, started.Error);
        Assert.NotNull(await harness.FindAsync(started.Response.ItemId));
    }

    [Fact]
    public async Task Upkeep_WithinTheClosingGrace_LeavesTheResponse_AndTheNextRunSubmitsIt()
    {
        // Arrange (R8-Mi8): a survey with a 60-second grace closes while u1's response is in progress.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = OnePage();
        definition.Timing = new SurveyTimingSettings { GraceSeconds = 60 };
        var closes = RuntimeTestHarness.Now.AddHours(1);
        var survey = await harness.PublishAsync(definition, item => item.Access.ClosesUtc = closes);
        var start = await harness.StartAsync(survey, "u1");

        // Act: one run inside the grace, one after it.
        harness.Clock.UtcNow = closes.AddSeconds(30);
        await RunUpkeepAsync(harness);
        var withinGrace = await harness.FindAsync(start.Response.ItemId);

        harness.Clock.UtcNow = closes.AddHours(1);
        await RunUpkeepAsync(harness);
        var nextRun = await harness.FindAsync(start.Response.ItemId);

        // Assert: the run inside the grace reads past it without counting a failure, and the next run submits it.
        Assert.Equal(SurveyResponseStatus.InProgress, withinGrace.Status);
        Assert.Equal(0, withinGrace.UpkeepFailures);
        Assert.Equal(SurveyResponseStatus.Completed, nextRun.Status);
        Assert.True(nextRun.AutoSubmitted);
    }

    [Fact]
    public async Task ResumableSteps_InScopes_BlameTheStepWhoseCommitFails_AndCommitTheOthers()
    {
        // Arrange (R8-Mi7): step A queues a write that fails at commit; step B, after it, reads first (which in a shared
        // session would flush A's write and fail as B) and then writes.
        var document = new ResumableStepsDocument();
        document.Pending[FailingStep.StepName] = new ResumableStepContext { Cursor = "a" };
        document.Pending[WritingStep.StepName] = new ResumableStepContext { Cursor = "b" };
        var documents = new Mock<IDocumentManager<ResumableStepsDocument>>();
        documents.Setup(manager => manager.GetOrCreateImmutableAsync(It.IsAny<Func<Task<ResumableStepsDocument>>>())).ReturnsAsync(document);
        documents.Setup(manager => manager.GetOrCreateMutableAsync(It.IsAny<Func<Task<ResumableStepsDocument>>>())).ReturnsAsync(document);
        var logger = new ListLogger();

        await using var harness = await RuntimeTestHarness.CreateAsync(services =>
        {
            SaveFailureIndexProvider<SurveyParticipation>.Register(services, new SaveFailureIndexProvider<SurveyParticipation>(
                SurveysConstants.CollectionName,
                item => item.SurveyId == FailingStep.SurveyId ? new InvalidOperationException("The save failed.") : null));
            services.AddSingleton(documents.Object);
            services.AddScoped<ResumableSteps>();
            services.AddScoped<IResumableStep, FailingStep>();
            services.AddScoped<IResumableStep, WritingStep>();
        });
        var store = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<IStore>()));

        await SaveFailureIndexProvider<SurveyParticipation>.CreateTableAsync(store, SurveysConstants.CollectionName);

        // Act
        var ran = await harness.RunInShellAsync(services => services.GetRequiredService<ResumableSteps>()
            .RunPendingInScopesAsync([FailingStep.StepName, WritingStep.StepName], logger, TestContext.Current.CancellationToken));

        // Assert: A is the step logged, and keeps its cursor; B ran, committed and is done.
        var rows = await harness.RunAsync(async services => (await services.GetRequiredService<ISession>()
            .QueryIndex<SurveyParticipationIndex>(collection: SurveysConstants.CollectionName)
            .ListAsync(TestContext.Current.CancellationToken)).ToList());

        Assert.Equal(1, ran);
        Assert.Contains(logger.Errors, error => error.Contains($"'{FailingStep.StepName}'", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Errors, error => error.Contains($"'{WritingStep.StepName}'", StringComparison.Ordinal));
        Assert.Equal(WritingStep.SurveyId, Assert.Single(rows).SurveyId);
        Assert.Equal("a", document.Pending[FailingStep.StepName].Cursor);
        Assert.False(document.Pending.ContainsKey(WritingStep.StepName));
    }

    private static SurveyDefinition OnePage()
        => RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));

    private static Task<bool> RunUpkeepAsync(RuntimeTestHarness harness)
        => harness.RunInShellAsync(async services =>
        {
            await services.GetRequiredService<SurveyMaintenanceService>().RunAsync(cancellationToken: TestContext.Current.CancellationToken);

            return true;
        });

    private static SurveyParticipation Participation(string surveyId)
        => new() { SurveyId = surveyId, RespondentKey = "u:u1", Day = DateOnly.FromDateTime(RuntimeTestHarness.Now), StartedCount = 1 };

    // Queues a participation record whose save fails when its session commits.
    private sealed class FailingStep : IResumableStep
    {
        public const string StepName = "Tests.Failing";

        public const string SurveyId = "fails";

        private readonly SurveyParticipationStore _store;

        public FailingStep(SurveyParticipationStore store)
        {
            _store = store;
        }

        public string Name => StepName;

        public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
        {
            await _store.CreateAsync(Participation(SurveyId), cancellationToken);
            context.Cursor = "a2";

            return true;
        }
    }

    // Reads, then writes a participation record that saves.
    private sealed class WritingStep : IResumableStep
    {
        public const string StepName = "Tests.Writing";

        public const string SurveyId = "kept";

        private readonly ISession _session;
        private readonly SurveyParticipationStore _store;

        public WritingStep(ISession session, SurveyParticipationStore store)
        {
            _session = session;
            _store = store;
        }

        public string Name => StepName;

        public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
        {
            await _session.QueryIndex<SurveyParticipationIndex>(collection: SurveysConstants.CollectionName).CountAsync(cancellationToken);
            await _store.CreateAsync(Participation(SurveyId), cancellationToken);

            return true;
        }
    }

    private sealed class ListLogger : ILogger
    {
        public List<string> Errors { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
        }
    }
}

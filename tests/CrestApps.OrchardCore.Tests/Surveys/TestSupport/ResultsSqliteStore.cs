using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A real SQLite store with the Surveys index tables and the answer index, for results tests.
/// </summary>
internal sealed class ResultsSqliteStore : IAsyncDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"surveys-results-{Guid.NewGuid():N}.db");
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());

    private ParameterLimitedSqliteConnectionFactory _connections;

    public IStore Store { get; private set; }

    /// <summary>
    /// Gets or sets the most parameters one statement may bind, or <see langword="null"/> for SQLite's own limit. Set it to
    /// <see cref="ParameterLimitedSqliteConnectionFactory.SqlServerLimit"/> once seeding is done, so a statement SQL Server
    /// would refuse fails here too.
    /// </summary>
    public int? ParameterLimit
    {
        get => _connections.ParameterLimit;
        set => _connections.ParameterLimit = value;
    }

    public static async Task<ResultsSqliteStore> CreateAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var result = new ResultsSqliteStore();
        var connectionString = $"Data Source={result._databasePath};Pooling=False";

        result._connections = new ParameterLimitedSqliteConnectionFactory(connectionString);
        result.Store = StoreFactory.Create(configuration =>
        {
            configuration.UseSqLite(connectionString);
            configuration.ConnectionFactory = result._connections;
        });
        result.Store.RegisterIndexes(
            [
                new SurveyIndexProvider(),
                new SurveyTagIndexProvider(),
                new SurveyVersionIndexProvider(),
                new SurveyCollectorIndexProvider(),
                new BankQuestionIndexProvider(),
                new BankQuestionTagIndexProvider(),
            ],
            SurveysConstants.CollectionName);
        result.Store.RegisterIndexes([new SurveyResponseIndexProvider()], SurveysConstants.ResponsesCollectionName);

        await result.Store.InitializeAsync(cancellationToken);
        await result.Store.InitializeCollectionAsync(SurveysConstants.CollectionName, cancellationToken);
        await result.Store.InitializeCollectionAsync(SurveysConstants.ResponsesCollectionName, cancellationToken);

        await using var session = result.Store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);
        var schema = new SchemaBuilder(result.Store.Configuration, transaction);

        await SurveysIndexSchema.CreateAsync(schema);
        await SurveyAnswerIndexSchema.CreateAsync(schema);
        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Opens a session that maps the answer index as a tenant's session does.
    /// </summary>
    /// <returns>The session.</returns>
    public ISession OpenSession()
    {
        var session = Store.CreateSession();
        session.RegisterIndexes([new SurveyAnswerIndexProvider(Services(session))], SurveysConstants.ResponsesCollectionName);

        return session;
    }

    /// <summary>
    /// Builds the services a session's scope has: the session, the control registry with the test type, the reader.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <returns>The services.</returns>
    public IServiceProvider Services(ISession session)
    {
        var services = new ServiceCollection();
        services.AddSingleton(session);
        services.AddSingleton<IMemoryCache>(_memoryCache);
        services.AddSingleton(new ShellSettings { Name = "Default" });
        services.AddSurveyControl<ResultsTestControlHandler>(ResultsTestControlHandler.Name);
        services.AddScoped<ISurveyControlRegistry, SurveyControlRegistry>();
        services.AddScoped<SurveyVersionDefinitionReader>();
        services.AddOptions<SurveyControlOptions>();

        return services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        _memoryCache.Dispose();
        TemporarySqliteDatabase.DisposeAndDelete(Store, _databasePath);

        await Task.CompletedTask;
    }

    public static SurveyQuestion ChoiceQuestion(string id, string name, params string[] choices)
        => new()
        {
            Id = id,
            Name = name,
            Title = name,
            ControlType = ResultsTestControlHandler.Name,
            Choices = choices.Select(choice => new SurveyChoice { Id = choice, Value = choice, Text = choice.ToUpperInvariant() }).ToList(),
        };

    public static SurveyQuestion NumberQuestion(string id, string name)
        => new()
        {
            Id = id,
            Name = name,
            Title = name,
            ControlType = ResultsTestControlHandler.Name,
        };

    public static SurveyVersion Version(string id, string surveyId, int number, params SurveyQuestion[] questions)
        => new()
        {
            ItemId = id,
            SurveyId = surveyId,
            Number = number,
            PublishedUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            Definition = new SurveyDefinition
            {
                Pages = [new SurveyPage { Id = "p1", Name = "page_1", Questions = [.. questions] }],
            },
        };

    public static SurveyResponse Response(string id, SurveyVersion version, SurveyResponseStatus status, DateTime completedUtc, params (string QuestionId, SurveyAnswer Answer)[] answers)
        => new()
        {
            ItemId = id,
            SurveyId = version.SurveyId,
            SurveyVersionId = version.ItemId,
            VersionNumber = version.Number,
            Status = status,
            CollectorId = "collector1",
            StartedUtc = completedUtc.AddMinutes(-5),
            LastActivityUtc = completedUtc,
            CompletedUtc = status == SurveyResponseStatus.InProgress ? null : completedUtc,
            ModifiedUtc = completedUtc,
            Answers = answers.ToDictionary(answer => answer.QuestionId, answer => answer.Answer),
        };
}

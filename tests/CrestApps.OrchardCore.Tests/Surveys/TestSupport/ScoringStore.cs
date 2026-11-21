using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A real SQLite store with the Surveys collections and index tables, deleted when disposed.
/// </summary>
internal sealed class ScoringStore : IAsyncDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"surveys-scoring-{Guid.NewGuid():N}.db");

    public IStore Store { get; private set; }

    public static async Task<ScoringStore> CreateAsync()
    {
        var harness = new ScoringStore();
        var cancellationToken = TestContext.Current.CancellationToken;

        harness.Store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={harness._databasePath};Pooling=False"));
        harness.Store.RegisterIndexes([new SurveyIndexProvider(), new SurveyVersionIndexProvider(), new SurveyOverrideIndexProvider()], SurveysConstants.CollectionName);
        harness.Store.RegisterIndexes([new SurveyResponseIndexProvider()], SurveysConstants.ResponsesCollectionName);

        await harness.Store.InitializeAsync(cancellationToken);
        await harness.Store.InitializeCollectionAsync(SurveysConstants.CollectionName, cancellationToken);
        await harness.Store.InitializeCollectionAsync(SurveysConstants.ResponsesCollectionName, cancellationToken);

        await using var session = harness.Store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);
        await SurveysIndexSchema.CreateAsync(new SchemaBuilder(harness.Store.Configuration, transaction));
        await transaction.CommitAsync(cancellationToken);

        return harness;
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(Store, _databasePath);

        return ValueTask.CompletedTask;
    }
}

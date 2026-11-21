using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Banks;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A SQLite store with the Surveys index tables, and the bank services over a session of it.
/// </summary>
internal sealed class BanksTestStore : IAsyncDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"surveys-banks-{Guid.NewGuid():N}.db");

    public IStore Store { get; private set; }

    public static async Task<BanksTestStore> CreateAsync(CancellationToken cancellationToken)
    {
        var result = new BanksTestStore();
        result.Store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={result._databasePath};Pooling=False"));
        result.Store.RegisterIndexes(
            [
                new SurveyIndexProvider(),
                new SurveyVersionIndexProvider(),
                new QuestionBankIndexProvider(),
                new BankQuestionIndexProvider(),
                new BankQuestionTagIndexProvider(),
            ],
            SurveysConstants.CollectionName);

        await result.Store.InitializeAsync(cancellationToken);
        await result.Store.InitializeCollectionAsync(SurveysConstants.CollectionName, cancellationToken);
        await result.Store.InitializeCollectionAsync(SurveysConstants.ResponsesCollectionName, cancellationToken);

        await using (var session = result.Store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            await SurveysIndexSchema.CreateAsync(new SchemaBuilder(result.Store.Configuration, transaction));
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    public static QuestionBankManager Manager(ISession session)
    {
        var bankStore = new QuestionBankStore(session);
        var questionStore = new BankQuestionStore(session);

        return new QuestionBankManager(
            new QuestionBankCatalogManager(bankStore, [], NullLogger<CrestApps.Core.Services.CatalogManager<CrestApps.OrchardCore.Surveys.Models.QuestionBank>>.Instance),
            new BankQuestionCatalogManager(questionStore, [], NullLogger<CrestApps.Core.Services.CatalogManager<CrestApps.OrchardCore.Surveys.Models.BankQuestion>>.Instance),
            bankStore,
            questionStore,
            new BanksControlRegistry(),
            RecipeRoundTrip.Localizer<QuestionBankManager>());
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(Store, _databasePath);

        return ValueTask.CompletedTask;
    }
}

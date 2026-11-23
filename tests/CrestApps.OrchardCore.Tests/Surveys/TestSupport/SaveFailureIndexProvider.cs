using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// An index provider whose map throws for the documents a test picks, so their save fails at flush time, as a
/// concurrency conflict, a deadlock or a truncated column does. YesSql then cancels the session.
/// </summary>
/// <typeparam name="T">The document type.</typeparam>
internal sealed class SaveFailureIndexProvider<T> : IndexProvider<T>
    where T : class
{
    private readonly Func<T, Exception> _failure;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveFailureIndexProvider{T}"/> class.
    /// </summary>
    /// <param name="collection">The collection the documents are kept in.</param>
    /// <param name="failure">Returns the exception a document's save throws, or <see langword="null"/> to save it.</param>
    public SaveFailureIndexProvider(string collection, Func<T, Exception> failure)
    {
        CollectionName = collection;
        _failure = failure;
    }

    public override void Describe(DescribeContext<T> context)
        => context.For<SaveFailureIndex>()
            .Map(item => _failure(item) is { } exception ? throw exception : (SaveFailureIndex)null);

    /// <summary>
    /// Registers the provider on every session of the test's services, and creates its (always empty) index table.
    /// </summary>
    /// <param name="services">The test's services.</param>
    /// <param name="provider">The provider.</param>
    public static void Register(IServiceCollection services, SaveFailureIndexProvider<T> provider)
        => services.AddScoped<ISession>(serviceProvider =>
        {
            var session = serviceProvider.GetRequiredService<IStore>().CreateSession();

            session.RegisterIndexes(provider);

            return session;
        });

    /// <summary>
    /// Creates the provider's index table in a collection.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="collection">The collection.</param>
    /// <returns>A task.</returns>
    public static async Task CreateTableAsync(IStore store, string collection)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<SaveFailureIndex>(table => table.Column<string>("Marker", column => column.WithLength(26)), collection);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }
}

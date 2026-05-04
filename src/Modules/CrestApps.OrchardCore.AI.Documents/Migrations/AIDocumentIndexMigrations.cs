using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.Indexing;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Documents.Migrations;

internal sealed class AIDocumentIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDocumentIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    public AIDocumentIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIDocumentIndexSchemaAsync(_option);

        return 3;
    }

    /// <summary>
    /// Updates the from1 async.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.CreateAIDocumentIndexSchemaAsync(_option);

        return 3;
    }

    /// <summary>
    /// Updates the from2 async.
    /// </summary>
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<AIDocumentIndex>(table =>
        {
            table.AddColumn<string>("FileName", column => column.WithLength(255));
        }, _option.AIDocsCollectionName);

        return 3;
    }
}

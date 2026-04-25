using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.Indexing;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Documents.Migrations;

internal sealed class AIDocumentIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    public AIDocumentIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIDocumentIndexSchemaAsync(_option);

        return 3;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.CreateAIDocumentIndexSchemaAsync(_option);

        return 3;
    }

    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<AIDocumentIndex>(table =>
        {
            table.AddColumn<string>("FileName", column => column.WithLength(255));
        }, _option.AIDocsCollectionName);

        return 3;
    }
}

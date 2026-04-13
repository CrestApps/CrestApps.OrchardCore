using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.Indexing;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

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
        await CreateAIDocumentIndexTableAsync();


        return 2;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await CreateAIDocumentIndexTableAsync();

        return 2;
    }

    private async Task CreateAIDocumentIndexTableAsync()
    {
        await SchemaBuilder.CreateAIDocumentIndexSchemaAsync(_option);
    }
}

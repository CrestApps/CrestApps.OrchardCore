using CrestApps.Core.Data.YesSql.Indexes.Indexing;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Documents.Migrations;

internal sealed class AIDocumentIndexMigrations : DataMigration
{
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
        await SchemaBuilder.CreateAIDocumentIndexSchemaAsync(AIConstants.AIDocsCollectionName);
    }
}

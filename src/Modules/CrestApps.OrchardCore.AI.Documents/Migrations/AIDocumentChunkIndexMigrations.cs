using CrestApps.Core.Data.YesSql.Indexes.Indexing;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Documents.Migrations;

internal sealed class AIDocumentChunkIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIDocumentChunkIndexSchemaAsync(AIConstants.AIDocsCollectionName);

        return 1;
    }
}

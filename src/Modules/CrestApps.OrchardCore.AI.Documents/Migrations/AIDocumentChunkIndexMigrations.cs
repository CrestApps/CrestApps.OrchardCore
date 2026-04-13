using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.Indexing;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Documents.Migrations;

internal sealed class AIDocumentChunkIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    public AIDocumentChunkIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIDocumentChunkIndexSchemaAsync(_option);

        return 1;
    }
}

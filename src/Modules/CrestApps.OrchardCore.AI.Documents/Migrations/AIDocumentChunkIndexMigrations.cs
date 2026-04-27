using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.Indexing;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Documents.Migrations;

internal sealed class AIDocumentChunkIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDocumentChunkIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    public AIDocumentChunkIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIDocumentChunkIndexSchemaAsync(_option);

        return 1;
    }
}

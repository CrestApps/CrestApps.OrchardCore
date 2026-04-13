using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AI;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

public sealed class AIDeploymentIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    public AIDeploymentIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIDeploymentIndexSchemaAsync(_option);

        return 1;
    }
}

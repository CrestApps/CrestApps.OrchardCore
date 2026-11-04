using CrestApps.OrchardCore.Workforce.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Workforce.Migrations;

internal sealed class LaborRulesMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await LaborRulesIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}

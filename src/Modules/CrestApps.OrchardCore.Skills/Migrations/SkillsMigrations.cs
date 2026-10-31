using CrestApps.OrchardCore.Skills.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Skills.Migrations;

internal sealed class SkillsMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SkillsIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}

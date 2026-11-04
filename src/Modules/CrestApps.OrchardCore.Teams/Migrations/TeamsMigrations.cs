using CrestApps.OrchardCore.Teams.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Teams.Migrations;

internal sealed class TeamsMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await TeamsIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}

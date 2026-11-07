using CrestApps.OrchardCore.Workforce.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Workforce.Migrations;

internal sealed class PortalMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await PortalIndexSchema.CreateAsync(SchemaBuilder);

        return 2;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await PortalIndexSchema.CreateShiftReminderLogAsync(SchemaBuilder);

        return 2;
    }
}

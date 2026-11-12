using CrestApps.OrchardCore.Workforce.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Workforce.Migrations;

internal sealed class PortalMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await PortalIndexSchema.CreateAsync(SchemaBuilder);
        await PortalIndexSchema.CreateStaffFeedIndexAsync(SchemaBuilder);

        return 3;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await PortalIndexSchema.CreateShiftReminderLogAsync(SchemaBuilder);

        return 2;
    }

    // Workers became staff members, kept in the Staff collection: their feed secrets are indexed there.
    public async Task<int> UpdateFrom2Async()
    {
        await PortalIndexSchema.CreateStaffFeedIndexAsync(SchemaBuilder);

        return 3;
    }
}

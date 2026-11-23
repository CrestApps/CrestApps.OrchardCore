using CrestApps.OrchardCore.MailingLists.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.MailingLists.Migrations;

/// <summary>
/// Creates the mailing lists tables: lists, members and the provider outbox.
/// </summary>
internal sealed class MailingListsMigrations : DataMigration
{
    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> CreateAsync()
    {
        await MailingListsIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}

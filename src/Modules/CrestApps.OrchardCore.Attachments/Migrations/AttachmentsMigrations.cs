using CrestApps.OrchardCore.Attachments.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Attachments.Migrations;

/// <summary>
/// Creates the index table of attachments.
/// </summary>
internal sealed class AttachmentsMigrations : DataMigration
{
    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> CreateAsync()
    {
        await AttachmentsIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}

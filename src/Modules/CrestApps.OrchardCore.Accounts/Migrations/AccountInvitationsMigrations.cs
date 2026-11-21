using CrestApps.OrchardCore.Accounts.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Accounts.Migrations;

/// <summary>
/// Creates the table of account invitations.
/// </summary>
internal sealed class AccountInvitationsMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await AccountInvitationIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}

using CrestApps.OrchardCore.Accounts.Indexes;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Accounts.Migrations;

/// <summary>
/// Defines the members part, adds it to the <c>Account</c> type, and creates the member index.
/// </summary>
internal sealed class AccountMembersMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public AccountMembersMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(AccountsConstants.ContentParts.Members, part => part
            .Attachable()
            .WithDisplayName("Account Members")
            .WithDescription("The users who sign in for an account, and what each of them may do for it."));

        await AccountTypeParts.AttachAsync(_contentDefinitionManager, AccountsConstants.ContentParts.Members, "4");

        await SchemaBuilder.CreateMapIndexTableAsync<AccountMemberIndex>(table => table
            .Column<string>("AccountContentItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<bool>("Published")
            .Column<bool>("Latest"));

        await SchemaBuilder.AlterIndexTableAsync<AccountMemberIndex>(table => table
            .CreateIndex("IDX_AccountMemberIndex_UserId", "UserId", "Published", "Latest"));

        return 1;
    }
}

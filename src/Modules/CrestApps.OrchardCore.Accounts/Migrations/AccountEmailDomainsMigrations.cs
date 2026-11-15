using CrestApps.OrchardCore.Accounts.Indexes;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Accounts.Migrations;

internal sealed class AccountEmailDomainsMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public AccountEmailDomainsMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(AccountsConstants.ContentParts.EmailDomains, part => part
            .Attachable()
            .WithDisplayName("Account E-mail Domains")
            .WithDescription("The e-mail domains an account's people write from, so their requests are filed under the account."));

        await AccountTypeParts.AttachAsync(_contentDefinitionManager, AccountsConstants.ContentParts.EmailDomains, "6");

        await SchemaBuilder.CreateMapIndexTableAsync<AccountEmailDomainIndex>(table => table
            .Column<string>("AccountContentItemId", column => column.WithLength(26))
            .Column<string>("Domain", column => column.WithLength(AccountEmailDomainsNormalizer.MaxLength)));

        await SchemaBuilder.AlterIndexTableAsync<AccountEmailDomainIndex>(table => table
            .CreateIndex("IDX_AccountEmailDomainIndex_Domain", "Domain", "AccountContentItemId"));

        return 1;
    }
}

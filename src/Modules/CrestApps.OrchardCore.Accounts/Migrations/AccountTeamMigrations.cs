using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Accounts.Migrations;

internal sealed class AccountTeamMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public AccountTeamMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(AccountsConstants.ContentParts.Team, part => part
            .Attachable()
            .WithDisplayName("Account Team")
            .WithDescription("The staff who look after an account: its owner and the rest of the account team."));

        await AccountTypeParts.AttachAsync(_contentDefinitionManager, AccountsConstants.ContentParts.Team, "6");

        return 1;
    }
}

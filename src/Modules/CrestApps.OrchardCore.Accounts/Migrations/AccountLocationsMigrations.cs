using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Accounts.Migrations;

/// <summary>
/// Defines the locations part and adds it to the <c>Account</c> type.
/// </summary>
internal sealed class AccountLocationsMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public AccountLocationsMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(AccountsConstants.ContentParts.Locations, part => part
            .Attachable()
            .WithDisplayName("Account Locations")
            .WithDescription("The places an account has, such as its head office, its warehouses or the sites where it is serviced."));

        await AccountTypeParts.AttachAsync(_contentDefinitionManager, AccountsConstants.ContentParts.Locations, "5");

        return 1;
    }
}

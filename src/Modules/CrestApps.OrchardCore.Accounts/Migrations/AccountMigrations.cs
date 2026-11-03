using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Accounts.Migrations;

/// <summary>
/// Defines the account part and creates the <c>Account</c> type when it is missing.
/// </summary>
/// <remarks>
/// A site that ran the Omnichannel CRM before accounts had a module of their own already has both, created under the
/// same names. The type is only created when it is missing, so that site's type, its settings and every account
/// stored with it are left exactly as they are.
/// </remarks>
internal sealed class AccountMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly AccountListSynchronizer _accountListSynchronizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="accountListSynchronizer">The synchronizer that knows which types can belong to an account.</param>
    public AccountMigrations(
        IContentDefinitionManager contentDefinitionManager,
        AccountListSynchronizer accountListSynchronizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _accountListSynchronizer = accountListSynchronizer;
    }

    /// <summary>
    /// Defines the account part and creates the <c>Account</c> type when it is missing.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(AccountsConstants.ContentParts.Account, part => part
            .Attachable()
            .WithDisplayName("Account")
            .WithDescription("Marks a content type as an account, the business or household that contacts, orders, assets and work orders belong to."));

        if (await _contentDefinitionManager.GetTypeDefinitionAsync(AccountsConstants.ContentTypes.Account) is null)
        {
            var childTypes = await _accountListSynchronizer.GetChildTypesAsync();

            await _contentDefinitionManager.AlterTypeDefinitionAsync(AccountsConstants.ContentTypes.Account, type => type
                .WithDisplayName("Account")
                .Creatable()
                .Listable()
                .Securable()
                .WithPart<TitlePart>(part => part
                    .WithPosition("1"))
                .WithPart(AccountsConstants.ContentParts.Account, part => part
                    .WithPosition("2")
                    .WithSettings(new AccountPartSettings
                    {
                        OfferedContentTypes = childTypes,
                    }))
                .WithPart(AccountsConstants.ContentParts.List, part => part
                    .WithPosition("3")
                    .WithSettings(new ListPartSettings
                    {
                        PageSize = 20,
                        ShowHeader = true,
                        ContainedContentTypes = childTypes,
                    })));
        }

        return 1;
    }
}

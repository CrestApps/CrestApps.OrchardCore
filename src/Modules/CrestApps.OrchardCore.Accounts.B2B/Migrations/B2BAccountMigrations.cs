using CrestApps.OrchardCore.Accounts.B2B.Indexes;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Accounts.B2B.Migrations;

/// <summary>
/// Defines the B2B part, adds it to the <c>Account</c> type, and creates the account and price list indexes.
/// </summary>
internal sealed class B2BAccountMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public B2BAccountMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(B2BAccountsConstants.ContentParts.B2BAccount, part => part
            .Attachable()
            .WithDisplayName("B2B Account")
            .WithDescription("Lets an account buy as a business: its payment terms, budget, price list, buyers' order limits and punchout."));

        // The part goes on the account type the Accounts feature creates; another account type can be given it by hand.
        if (await _contentDefinitionManager.GetTypeDefinitionAsync(AccountsConstants.ContentTypes.Account) is { } accountType &&
            !accountType.Parts.Any(part => part.PartDefinition?.Name == B2BAccountsConstants.ContentParts.B2BAccount))
        {
            await _contentDefinitionManager.AlterTypeDefinitionAsync(AccountsConstants.ContentTypes.Account, type => type
                .WithPart(B2BAccountsConstants.ContentParts.B2BAccount, part => part
                    .WithPosition("10")));
        }

        await SchemaBuilder.CreateMapIndexTableAsync<B2BAccountIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<bool>("Enabled")
            .Column<string>("PriceListId", column => column.Nullable().WithLength(26))
            .Column<bool>("Published")
            .Column<bool>("Latest"));

        await SchemaBuilder.AlterIndexTableAsync<B2BAccountIndex>(table => table
            .CreateIndex("IDX_B2BAccountIndex_ContentItemId", "ContentItemId", "Published", "Latest"));

        await SchemaBuilder.AlterIndexTableAsync<B2BAccountIndex>(table => table
            .CreateIndex("IDX_B2BAccountIndex_PriceListId", "PriceListId", "Latest"));

        await SchemaBuilder.CreateMapIndexTableAsync<PriceListIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255)),
            collection: B2BAccountsConstants.PriceListsCollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<PriceListIndex>(table => table
            .CreateIndex("IDX_PriceListIndex_ItemId", "ItemId"),
            collection: B2BAccountsConstants.PriceListsCollectionName
        );

        return 1;
    }
}

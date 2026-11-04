using CrestApps.OrchardCore.Accounts.B2B;
using CrestApps.OrchardCore.Accounts.B2B.Indexes;
using CrestApps.OrchardCore.Accounts.B2B.Migrations;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.Indexes;
using CrestApps.OrchardCore.Accounts.Migrations;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Tests.Modules.Accounts;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Commerce.B2B;

/// <summary>
/// The checkout finds the account a signed-in buyer orders for from its published version, and a price list's accounts
/// are found when it is deleted.
/// </summary>
public sealed class B2BAccountStoreTests
{
    [Fact]
    public async Task FindByBuyerAsync_FindsThePublishedAccountTheUserBuysFor()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-b2b-{Guid.NewGuid():N}.db");
        var yesSql = await CreateStoreAsync(path);

        try
        {
            await using (var session = yesSql.CreateSession())
            {
                await session.SaveAsync(TestB2BAccounts.CreateItem("account-1", "Northwind Traders", part => part.PriceListId = "list-1", members: [new AccountMember { UserId = "user-1" }]), cancellationToken: TestContext.Current.CancellationToken);

                // A draft account doesn't buy yet, so its buyers buy for themselves.
                await session.SaveAsync(TestB2BAccounts.CreateItem("account-2", "Contoso", published: false, members: [new AccountMember { UserId = "user-2" }]), cancellationToken: TestContext.Current.CancellationToken);

                // An account without the part isn't indexed at all.
                var plain = new ContentItem { ContentItemId = "account-3", ContentItemVersionId = "account-3-v1", ContentType = "Account", Published = true, Latest = true };
                await session.SaveAsync(plain, cancellationToken: TestContext.Current.CancellationToken);

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = yesSql.CreateSession())
            {
                var store = new B2BAccountStore(session, Mock.Of<IContentManager>(), new AccountMemberService(session));

                var account = await store.FindByBuyerAsync("user-1", TestContext.Current.CancellationToken);

                Assert.Equal("account-1", account?.AccountId);
                Assert.Equal("Northwind Traders", account.Name);
                Assert.True(account.IsEnabled);
                Assert.Null(await store.FindByBuyerAsync("user-2", TestContext.Current.CancellationToken));
                Assert.Null(await store.FindByBuyerAsync("nobody", TestContext.Current.CancellationToken));
                Assert.Null(await store.FindByBuyerAsync(null, TestContext.Current.CancellationToken));
                Assert.Equal(["account-1"], (await store.ListByPriceListAsync("list-1", TestContext.Current.CancellationToken)).Select(entry => entry.AccountId));
                Assert.Empty(await store.ListByPriceListAsync("list-2", TestContext.Current.CancellationToken));
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(yesSql, path);
        }
    }

    [Fact]
    public async Task PriceLists_ListByName()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-b2b-{Guid.NewGuid():N}.db");
        var yesSql = await CreateStoreAsync(path);

        try
        {
            await using (var session = yesSql.CreateSession())
            {
                var priceLists = new PriceListStore(session);

                await priceLists.CreateAsync(new PriceList { ItemId = "p1", Name = "Wholesale" }, TestContext.Current.CancellationToken);
                await priceLists.CreateAsync(new PriceList { ItemId = "p2", Name = "Distributors" }, TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = yesSql.CreateSession())
            {
                var priceLists = new PriceListStore(session);

                Assert.Equal(["p2", "p1"], (await priceLists.ListAllAsync(TestContext.Current.CancellationToken)).Select(list => list.ItemId));
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(yesSql, path);
        }
    }

    [Fact]
    public async Task TheMigration_AddsThePartToTheAccountType_Once()
    {
        var definitions = new InMemoryContentDefinitionManager();
        await definitions.StoreTypeDefinitionAsync(new global::OrchardCore.ContentManagement.Metadata.Builders.ContentTypeDefinitionBuilder()
            .WithName("Account")
            .WithPart("TitlePart")
            .WithPart("AccountPart")
            .Build());

        var path = Path.Combine(Path.GetTempPath(), $"crestapps-b2b-{Guid.NewGuid():N}.db");
        var yesSql = await CreateStoreAsync(path, definitions);

        try
        {
            var account = await definitions.GetTypeDefinitionAsync("Account");

            Assert.Single(account.Parts, part => part.Name == B2BAccountsConstants.ContentParts.B2BAccount);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(yesSql, path);
        }
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath, InMemoryContentDefinitionManager definitions = null)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new B2BAccountIndexProvider(), new AccountMemberIndexProvider()]);
        store.RegisterIndexes([new PriceListIndexProvider()], B2BAccountsConstants.PriceListsCollectionName);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(B2BAccountsConstants.PriceListsCollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migration = new B2BAccountMigrations(definitions ?? new InMemoryContentDefinitionManager())
        {
            SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
        };
        await migration.CreateAsync();
        await new AccountMembersMigrations(definitions ?? new InMemoryContentDefinitionManager())
        {
            SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
        }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}

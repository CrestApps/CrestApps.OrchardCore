using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.Indexes;
using CrestApps.OrchardCore.Accounts.Migrations;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Tests.Commerce.B2B;
using CrestApps.OrchardCore.Tests.Utilities;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Builders;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Accounts;

/// <summary>
/// An account's members are the users who sign in for it: the member index finds a user's account, and B2B reads its
/// buyers from them.
/// </summary>
public sealed class AccountMembersTests
{
    [Fact]
    public async Task FindAccountAsync_FindsThePublishedAccountTheUserIsAMemberOf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-account-members-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(path);

        try
        {
            await using (var session = store.CreateSession())
            {
                await session.SaveAsync(Account("account-1", published: true, new AccountMember { UserId = "user-1" }, new AccountMember { UserId = "user-1" }), cancellationToken: TestContext.Current.CancellationToken);
                await session.SaveAsync(Account("account-2", published: false, new AccountMember { UserId = "user-2" }, new AccountMember { UserName = "no-user-id" }), cancellationToken: TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = store.CreateSession())
            {
                var service = new AccountMemberService(session);

                Assert.Equal("account-1", (await service.FindAccountAsync("user-1", TestContext.Current.CancellationToken))?.ContentItemId);

                // A draft account has no published members yet.
                Assert.Null(await service.FindAccountAsync("user-2", TestContext.Current.CancellationToken));
                Assert.Null(await service.FindAccountAsync("nobody", TestContext.Current.CancellationToken));
                Assert.Null(await service.FindAccountAsync(null, TestContext.Current.CancellationToken));

                // The duplicate member and the member without a user are left out of the index.
                var rows = await session.QueryIndex<AccountMemberIndex>().ListAsync(TestContext.Current.CancellationToken);

                Assert.Equal(["account-1:user-1", "account-2:user-2"], rows.Select(row => $"{row.AccountContentItemId}:{row.UserId}").Order(StringComparer.Ordinal));
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public async Task FindOtherAccountIdAsync_TellsAnEditorTheUserBelongsToAnotherAccount()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-account-members-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(path);

        try
        {
            await using (var session = store.CreateSession())
            {
                // A draft counts: the user can't be added to a second account while one is being set up.
                await session.SaveAsync(Account("account-1", published: false, new AccountMember { UserId = "user-1" }), cancellationToken: TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = store.CreateSession())
            {
                var service = new AccountMemberService(session);

                Assert.Equal("account-1", await service.FindOtherAccountIdAsync("user-1", "account-2", TestContext.Current.CancellationToken));
                Assert.Null(await service.FindOtherAccountIdAsync("user-1", "account-1", TestContext.Current.CancellationToken));
                Assert.Null(await service.FindOtherAccountIdAsync("user-9", "account-2", TestContext.Current.CancellationToken));
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public async Task TheMembersMigration_AddsThePartToTheAccountType_Once()
    {
        // Arrange
        var definitions = new InMemoryContentDefinitionManager();
        await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder().WithName("Account").WithPart("TitlePart").WithPart("AccountPart").Build());
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-account-members-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={path};Pooling=False"));
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        try
        {
            // Act
            await using (var session = store.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                await new AccountMembersMigrations(definitions) { SchemaBuilder = new SchemaBuilder(store.Configuration, transaction) }.CreateAsync();
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            await new AccountLocationsMigrations(definitions).CreateAsync();
            await new AccountLocationsMigrations(definitions).CreateAsync();

            // Assert
            var account = await definitions.GetTypeDefinitionAsync("Account");

            Assert.Equal(["TitlePart", "AccountPart", "AccountMembersPart", "AccountLocationsPart"], account.Parts.Select(part => part.Name));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public void ABusinessAccountsBuyers_AreItsMembers_WithTheirOrderLimits()
    {
        // Arrange - a limit for a user who left the account, and one that isn't a limit at all, are ignored.
        var account = TestB2BAccounts.Create(
            configure: part => part.OrderLimits =
            [
                new BuyerOrderLimit { UserId = "user-2", Limit = 250m },
                new BuyerOrderLimit { UserId = "user-1", Limit = 0m },
                new BuyerOrderLimit { UserId = "user-gone", Limit = 100m },
            ],
            members:
            [
                new AccountMember { UserId = "user-1", Email = "ada@contoso.example", Role = AccountMemberRole.Admin },
                new AccountMember { UserId = "user-2", Email = "bob@contoso.example" },
            ],
            locations: [new AccountLocation { LocationId = "hq", Name = "Head office" }]);

        // Assert
        Assert.Equal(["user-1", "user-2"], account.Buyers.Select(buyer => buyer.UserId));
        Assert.Null(account.FindBuyer("user-1").OrderLimit);
        Assert.Equal(AccountMemberRole.Admin, account.FindBuyer("user-1").Role);
        Assert.Equal(250m, account.FindBuyer("user-2").OrderLimit);
        Assert.Null(account.FindBuyer("user-gone"));
        Assert.Equal("Head office", Assert.Single(account.Locations).Name);
    }

    [Fact]
    public void AnAccountWithoutMembersOrLocations_HasNoBuyersOrLocations()
    {
        var account = TestB2BAccounts.Create();

        Assert.Empty(account.Buyers);
        Assert.Empty(account.Locations);
    }

    private static ContentItem Account(string id, bool published, params AccountMember[] members)
    {
        var contentItem = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v1",
            ContentType = "Account",
            Published = published,
            Latest = true,
        };

        contentItem.Weld<AccountPart>();
        contentItem.Apply(new AccountMembersPart { Members = [.. members] });

        return contentItem;
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new AccountMemberIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new AccountMembersMigrations(new InMemoryContentDefinitionManager())
        {
            SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
        }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}

using CrestApps.OrchardCore.Companies;
using CrestApps.OrchardCore.Companies.Indexes;
using CrestApps.OrchardCore.Companies.Migrations;
using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Companies.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// The companies and price lists admin pages list records by name, finding them by any part of it.
/// </summary>
public sealed class CompanyStoreTests
{
    [Fact]
    public async Task ThePages_ListByName_AndFindByAnyPartOfIt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-companies-{Guid.NewGuid():N}.db");
        var yesSql = await CreateStoreAsync(path);

        try
        {
            await using (var session = yesSql.CreateSession())
            {
                var companies = new CompanyStore(session);
                var priceLists = new PriceListStore(session);

                await companies.CreateAsync(new Company { ItemId = "c1", Name = "Northwind Traders" }, TestContext.Current.CancellationToken);
                await companies.CreateAsync(new Company { ItemId = "c2", Name = "Contoso" }, TestContext.Current.CancellationToken);
                await companies.CreateAsync(new Company { ItemId = "c3", Name = "Fabrikam Traders" }, TestContext.Current.CancellationToken);
                await priceLists.CreateAsync(new PriceList { ItemId = "p1", Name = "Wholesale" }, TestContext.Current.CancellationToken);
                await priceLists.CreateAsync(new PriceList { ItemId = "p2", Name = "Distributors" }, TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = yesSql.CreateSession())
            {
                var companies = new CompanyStore(session);
                var priceLists = new PriceListStore(session);

                var (allCount, all) = await companies.PageAsync(null, 0, 2, TestContext.Current.CancellationToken);
                var (tradersCount, traders) = await companies.PageAsync("  Traders ", 0, 10, TestContext.Current.CancellationToken);

                Assert.Equal(3, allCount);
                Assert.Equal(["c2", "c3"], all.Select(company => company.ItemId));
                Assert.Equal(2, tradersCount);
                Assert.Equal(["c3", "c1"], traders.Select(company => company.ItemId));
                Assert.Equal(["p2", "p1"], (await priceLists.ListAllAsync(TestContext.Current.CancellationToken)).Select(list => list.ItemId));
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(yesSql, path);
        }
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new CompanyIndexProvider(), new PriceListIndexProvider()], CompaniesConstants.CollectionName);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(CompaniesConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migration = new CompanyMigrations { SchemaBuilder = new SchemaBuilder(store.Configuration, transaction) };
        await migration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}

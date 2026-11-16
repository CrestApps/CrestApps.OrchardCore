using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.FileProviders;
using OrchardCore.Autoroute.Core.Indexes;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// H2 (final security review): the reserved prefixes named nine words, so a buyer could name a site <c>account</c> and
/// take every URL under the store's <c>/account</c>. A prefix is now refused when one of the store's routes starts with it
/// (read from ASP.NET Core's endpoint data source), or one of its pages' paths does (read from Orchard's Autoroute index
/// on a real SQLite database).
/// </summary>
public sealed class StoreRouteSegmentsTests
{
    [Theory]
    [InlineData("account")]
    [InlineData("Account")]
    [InlineData("book")]
    public async Task IsTakenAsync_TheFirstSegmentOfAStoreRoute_IsTaken(string segment)
    {
        var routes = StoreRoutes.Create("account/addresses", "book/{slug}", "{area}/{controller}/{action}", "cart");

        Assert.True(await routes.IsTakenAsync(segment, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("shop1")]
    [InlineData("addresses")]
    public async Task IsTakenAsync_ASegmentNoRouteStartsWith_IsFree(string segment)
    {
        var routes = StoreRoutes.Create("account/addresses", "{area}/{controller}/{action}");

        Assert.False(await routes.IsTakenAsync(segment, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("about", true)]
    [InlineData("company", true)]
    [InlineData("comp", false)]
    [InlineData("shop1", false)]
    public async Task IsTakenAsync_TheFirstSegmentOfAStorePagePath_IsTaken(string segment, bool taken)
        => Assert.Equal(taken, await IsTakenByPagesAsync(segment, exactPaths: false, "about", "company/team"));

    // MD2 (fix verification): the page paths were compared with the database's collation, which is exact on PostgreSQL,
    // so a store page with the permalink "Pricing" was not found for a site named "pricing". YesSql makes SQLite's text
    // columns NOCASE, so the index table here is made with an exact Path column, as PostgreSQL compares it.
    [Theory]
    [InlineData("pricing", "Pricing")]
    [InlineData("pricing", "/Pricing/Plans")]
    [InlineData("PRICING", "pricing")]
    [InlineData("Docs", "docs/getting-started")]
    public async Task IsTakenAsync_APagePathInAnotherCase_IsTaken(string segment, string pagePath)
        => Assert.True(await IsTakenByPagesAsync(segment, exactPaths: true, pagePath));

    [Fact]
    public async Task IsTakenAsync_APagePathThatOnlySharesTheFirstLetter_IsFree()
        => Assert.False(await IsTakenByPagesAsync("pricing", exactPaths: true, "Prices", "p", "/pricingplans"));

    // MD2 (fix verification): Orchard Core serves a module's or theme's wwwroot under /{ModuleId}/ with the static file
    // middleware, which is not an endpoint, so a site named "TheAdmin" took the store admin's scripts and styles.
    [Theory]
    [InlineData("TheAdmin")]
    [InlineData("theadmin")]
    [InlineData("crestapps_resources")]
    public async Task IsTakenAsync_AModuleOrThemeId_IsTaken(string segment)
    {
        var routes = StoreRoutes.Create(session: null, ["TheAdmin", "crestapps_resources", "OrchardCore.Resources"], webRoot: null, "cart");

        Assert.True(await routes.IsTakenAsync(segment, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("assets", true)]
    [InlineData("Assets", true)]
    [InlineData("robots", false)]
    [InlineData("shop1", false)]
    public async Task IsTakenAsync_ATopLevelEntryOfTheWebRoot_IsTaken(string segment, bool taken)
    {
        var root = Path.Combine(Path.GetTempPath(), $"crestapps-store-webroot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "assets", "js"));
        await File.WriteAllTextAsync(Path.Combine(root, "robots.txt"), "User-agent: *", TestContext.Current.CancellationToken);

        try
        {
            using var webRoot = new PhysicalFileProvider(root);
            var routes = StoreRoutes.Create(session: null, ["TheAdmin"], webRoot, "cart");

            Assert.Equal(taken, await routes.IsTakenAsync(segment, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<bool> IsTakenByPagesAsync(string segment, bool exactPaths, params string[] pagePaths)
    {
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-store-routes-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={path};Pooling=False"));

        try
        {
            store.RegisterIndexes([new PageIndexProvider()]);
            await store.InitializeAsync(TestContext.Current.CancellationToken);

            await using (var session = store.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);

                if (exactPaths)
                {
                    await using var command = transaction.Connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText =
                        "CREATE TABLE AutoroutePartIndex (Id INTEGER PRIMARY KEY AUTOINCREMENT, DocumentId INTEGER, ContentItemId TEXT, " +
                        "ContainedContentItemId TEXT, JsonPath TEXT, Path TEXT COLLATE BINARY, Published BOOLEAN, Latest BOOLEAN)";
                    await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
                }
                else
                {
                    await new SchemaBuilder(store.Configuration, transaction).CreateMapIndexTableAsync<AutoroutePartIndex>(table => table
                        .Column<string>("ContentItemId", column => column.WithLength(26))
                        .Column<string>("ContainedContentItemId", column => column.WithLength(26))
                        .Column<string>("JsonPath", column => column.Unlimited())
                        .Column<string>("Path", column => column.WithLength(1024))
                        .Column<bool>("Published")
                        .Column<bool>("Latest"));
                }

                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = store.CreateSession())
            {
                foreach (var pagePath in pagePaths)
                {
                    await session.SaveAsync(new Page { Path = pagePath }, cancellationToken: TestContext.Current.CancellationToken);
                }

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = store.CreateSession())
            {
                return await StoreRoutes.Create(session, "cart").IsTakenAsync(segment, TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    private sealed class Page
    {
        public string Path { get; set; }
    }

    private sealed class PageIndexProvider : IndexProvider<Page>
    {
        public override void Describe(DescribeContext<Page> context)
            => context.For<AutoroutePartIndex>().Map(page => new AutoroutePartIndex
            {
                ContentItemId = "item",
                Path = page.Path,
                Published = true,
                Latest = true,
            });
    }
}

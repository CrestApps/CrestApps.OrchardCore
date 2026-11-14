using CrestApps.OrchardCore.Tests.Utilities;
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
                await new SchemaBuilder(store.Configuration, transaction).CreateMapIndexTableAsync<AutoroutePartIndex>(table => table
                    .Column<string>("ContentItemId", column => column.WithLength(26))
                    .Column<string>("ContainedContentItemId", column => column.WithLength(26))
                    .Column<string>("JsonPath", column => column.Unlimited())
                    .Column<string>("Path", column => column.WithLength(1024))
                    .Column<bool>("Published")
                    .Column<bool>("Latest"));
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = store.CreateSession())
            {
                await session.SaveAsync(new Page { Path = "about" }, cancellationToken: TestContext.Current.CancellationToken);
                await session.SaveAsync(new Page { Path = "company/team" }, cancellationToken: TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = store.CreateSession())
            {
                var routes = StoreRoutes.Create(session, "cart");

                Assert.Equal(taken, await routes.IsTakenAsync(segment, TestContext.Current.CancellationToken));
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

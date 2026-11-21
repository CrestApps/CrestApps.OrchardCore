using System.Globalization;
using System.Security.Claims;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.OrchardCore.AI.Mcp.Handlers;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Json;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Mcp;

/// <summary>
/// SEC-A14: the content-type resource lists the published items of a type a page at a time, against a YesSql store on
/// SQLite. It leaves out the items the caller may not view, and because it filters a page after reading it, it says
/// when more items may follow so a short page is not taken for the last one.
/// </summary>
public sealed class ContentByTypeResourceListTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-mcp-content-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new ContentItemIndexProvider()]);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<ContentItemIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentItemVersionId", column => column.WithLength(26))
            .Column<bool>("Published")
            .Column<bool>("Latest")
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<DateTime>("ModifiedUtc")
            .Column<DateTime>("PublishedUtc")
            .Column<DateTime>("CreatedUtc")
            .Column<string>("Owner", column => column.WithLength(255))
            .Column<string>("Author", column => column.WithLength(255))
            .Column<string>("DisplayText", column => column.WithLength(255)));

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AnItemTheCallerMayNotView_IsLeftOut()
    {
        // Arrange
        await SaveAsync(Item("open-1", minutesAgo: 1), Item("secret-1", minutesAgo: 2), Item("open-2", minutesAgo: 3));

        // Act
        var text = await ReadAsync(page: null, deniedIds: ["secret-1"]);

        // Assert
        Assert.Contains("open-1", text, StringComparison.Ordinal);
        Assert.Contains("open-2", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-1", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APageThatIsNotTheLast_SaysWhichPageToReadNext_EvenWhenItemsWereLeftOut()
    {
        // Arrange: one page and one more item; one item of the first page is left out.
        var items = Enumerable.Range(0, ContentByTypeResourceTypeHandler.PageSize + 1)
            .Select(index => Item("item-" + index.ToString("D3", CultureInfo.InvariantCulture), minutesAgo: index))
            .ToArray();
        await SaveAsync(items);

        // Act
        var first = await ReadAsync(page: null, deniedIds: ["item-005"]);
        var last = await ReadAsync(page: "2", deniedIds: ["item-005"]);

        // Assert
        Assert.Contains("page 2", first, StringComparison.Ordinal);
        Assert.Contains("item-050", last, StringComparison.Ordinal);
        Assert.DoesNotContain("page 3", last, StringComparison.Ordinal);
    }

    private async Task<string> ReadAsync(string page, string[] deniedIds)
    {
        await using var session = _store.CreateSession();

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object resource, IEnumerable<IAuthorizationRequirement> _) =>
                resource is ContentItem item && deniedIds.Contains(item.ContentItemId) ? AuthorizationResult.Failed() : AuthorizationResult.Success());

        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "caller")], "Api")) },
        };

        var handler = new ContentByTypeResourceTypeHandler(
            session,
            authorization.Object,
            accessor,
            Options.Create(new DocumentJsonSerializerOptions()),
            NullLogger<ContentByTypeResourceTypeHandler>.Instance);

        var variables = new Dictionary<string, string> { ["contentType"] = "Article" };

        if (page is not null)
        {
            variables["page"] = page;
        }

        var result = await handler.ReadAsync(
            new McpResource { ItemId = "r1", Resource = new Resource { Uri = "content://Article", Name = "articles" } },
            variables,
            TestContext.Current.CancellationToken);

        return string.Concat(result.Contents.OfType<TextResourceContents>().Select(content => content.Text));
    }

    private async Task SaveAsync(params ContentItem[] items)
    {
        await using var session = _store.CreateSession();

        foreach (var item in items)
        {
            await session.SaveAsync(item, cancellationToken: TestContext.Current.CancellationToken);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static ContentItem Item(string id, int minutesAgo)
        => new()
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v",
            ContentType = "Article",
            Published = true,
            Latest = true,
            PublishedUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(-minutesAgo),
        };
}

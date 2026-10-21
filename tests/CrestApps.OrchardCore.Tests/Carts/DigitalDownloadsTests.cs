using System.Text;
using CrestApps.OrchardCore.Downloads;
using CrestApps.OrchardCore.Downloads.Handlers;
using CrestApps.OrchardCore.Downloads.Indexes;
using CrestApps.OrchardCore.Downloads.Migrations;
using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Downloads.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.FileStorage;
using OrchardCore.FileStorage.FileSystem;
using OrchardCore.Infrastructure;
using OrchardCore.Locking;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Carts;

/// <summary>
/// A buyer of a digital product downloads it through their link until the link runs out, expires, or the order is
/// refunded; the files sit in a private store that only those links reach.
/// </summary>
public sealed class DigitalDownloadsTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-downloads-{Guid.NewGuid():N}.db");
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"crestapps-digital-files-{Guid.NewGuid():N}");
    private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();
    private readonly LocalLock _lock = new(NullLogger<LocalLock>.Instance);
    private DateTime _clockNow = _now;
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new DownloadEntitlementIndexProvider()], DownloadsConstants.CollectionName);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);
        await _store.InitializeCollectionAsync(DownloadsConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new DownloadMigrations(Mock.Of<IContentDefinitionManager>()) { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task TheFileStore_KeepsAFileByIdWithItsName_AndRefusesIdsItDidNotMake()
    {
        var store = FileStore();

        var saved = await store.SaveAsync("..\\..\\Guide: chapter 1.pdf", new MemoryStream(Encoding.UTF8.GetBytes("hello")), TestContext.Current.CancellationToken);

        Assert.Equal("Guide_ chapter 1.pdf", saved.FileName);
        Assert.Equal(5, saved.Size);

        var found = await store.FindAsync(saved.FileId, TestContext.Current.CancellationToken);
        Assert.Equal(saved, found);

        await using (var stream = await store.OpenAsync(saved.FileId, TestContext.Current.CancellationToken))
        {
            Assert.Equal("hello", await new StreamReader(stream).ReadToEndAsync(TestContext.Current.CancellationToken));
        }

        Assert.Null(await store.FindAsync("../secrets", TestContext.Current.CancellationToken));
        Assert.Null(await store.FindAsync("nosuchfile", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheFileStore_RefusesAFileAFileEventHandlerRejects_AndKeepsNothing()
    {
        // Arrange
        var handler = new Mock<IFileEventHandler>();
        handler
            .Setup(value => value.CreatingAsync(It.IsAny<FileCreatingContext>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileCreatingContext context, Stream stream, CancellationToken cancellationToken) => FileCreatingResult.Failed(stream, new ResultError
            {
                Message = new Microsoft.Extensions.Localization.LocalizedString("infected", "The file is infected."),
            }));

        var store = FileStore(handler.Object);

        // Act
        var exception = await Assert.ThrowsAsync<FileStoreException>(() => store.SaveAsync("guide.pdf", new MemoryStream([1, 2, 3]), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("The file is infected.", exception.Message);
        Assert.False(Directory.Exists(_folder) && Directory.EnumerateFiles(_folder, "*", SearchOption.AllDirectories).Any());
        handler.Verify(value => value.CreatedAsync(It.IsAny<IFileStoreEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheFileStore_TellsTheFileEventHandlersAboutAFileItKept()
    {
        // Arrange
        var handler = new Mock<IFileEventHandler>();
        handler
            .Setup(value => value.CreatingAsync(It.IsAny<FileCreatingContext>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileCreatingContext context, Stream stream, CancellationToken cancellationToken) => FileCreatingResult.Success(stream));

        var store = FileStore(handler.Object);

        // Act
        var saved = await store.SaveAsync("guide.pdf", new MemoryStream([1, 2, 3]), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, saved.Size);
        handler.Verify(value => value.CreatingAsync(It.Is<FileCreatingContext>(context => context.FileName == "guide.pdf" && context.Length == 3), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        handler.Verify(value => value.CreatedAsync(It.Is<IFileStoreEntry>(entry => entry.Name == "guide.pdf"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ABuyer_DownloadsThroughTheirLink_UntilTheLimit()
    {
        var file = await FileStore().SaveAsync("guide.pdf", new MemoryStream([1, 2, 3]), TestContext.Current.CancellationToken);
        var order = PaidOrder();

        await InSessionAsync(service => service.GrantAsync(order, TestContext.Current.CancellationToken), Product(file, limit: 2));

        // Granting again adds nothing.
        Assert.Equal(0, await InSessionAsync(service => service.GrantAsync(order, TestContext.Current.CancellationToken), Product(file, limit: 2)));

        var url = await InSessionAsync(async service => await service.CreateUrlAsync(Assert.Single(await service.ListAsync(order.ItemId, TestContext.Current.CancellationToken))));
        var token = Uri.UnescapeDataString(url[(url.LastIndexOf('/') + 1)..]);

        Assert.StartsWith("https://store.test/downloads/", url, StringComparison.Ordinal);

        Assert.True(await DownloadAsync(token));
        Assert.True(await DownloadAsync(token));
        Assert.False(await DownloadAsync(token));

        var (entitlement, problem) = await InSessionAsync(service => service.ResolveAsync(token, TestContext.Current.CancellationToken));
        Assert.Equal(DownloadProblem.LimitReached, problem);
        Assert.Equal(2, entitlement.DownloadCount);

        // A changed link names nothing.
        Assert.Equal(DownloadProblem.NotFound, (await InSessionAsync(service => service.ResolveAsync(token + "x", TestContext.Current.CancellationToken))).Problem);
    }

    [Fact]
    public async Task ALink_StopsWhenItExpires_AndWhenTheOrderIsRefundedInFull()
    {
        var file = await FileStore().SaveAsync("guide.pdf", new MemoryStream([1]), TestContext.Current.CancellationToken);
        var order = PaidOrder();

        await InSessionAsync(service => service.GrantAsync(order, TestContext.Current.CancellationToken), Product(file, limit: 0, days: 10));

        var entitlement = Assert.Single(await InSessionAsync(service => service.ListAsync(order.ItemId, TestContext.Current.CancellationToken)));
        Assert.Null(entitlement.DownloadLimit);
        Assert.Equal(DownloadProblem.None, DownloadService.GetProblem(entitlement, _now.AddDays(9)));
        Assert.Equal(DownloadProblem.Expired, DownloadService.GetProblem(entitlement, _now.AddDays(10)));

        // A partial refund leaves the files; a full one takes them back.
        order.Refunds.Add(new OrderRefund { RefundId = "r1", Amount = 5m });
        await InSessionAsync(async service => { await new DownloadOrderEventHandler(service, new PassThroughLocalizer<DownloadOrderEventHandler>()).RefundedAsync(new OrderRefundedContext(order, order.Refunds[0]), TestContext.Current.CancellationToken); return 0; });
        Assert.Null(Assert.Single(await InSessionAsync(service => service.ListAsync(order.ItemId, TestContext.Current.CancellationToken))).RevokedUtc);

        order.Refunds.Add(new OrderRefund { RefundId = "r2", Amount = 15m });
        await InSessionAsync(async service => { await new DownloadOrderEventHandler(service, new PassThroughLocalizer<DownloadOrderEventHandler>()).RefundedAsync(new OrderRefundedContext(order, order.Refunds[1]), TestContext.Current.CancellationToken); return 0; });

        var revoked = Assert.Single(await InSessionAsync(service => service.ListAsync(order.ItemId, TestContext.Current.CancellationToken)));
        Assert.Equal(DownloadProblem.Revoked, DownloadService.GetProblem(revoked, _now));

        // The store can give them back.
        await InSessionAsync(async service => { await service.ResetAsync(revoked, TestContext.Current.CancellationToken); return 0; });
        Assert.Equal(DownloadProblem.None, DownloadService.GetProblem(Assert.Single(await InSessionAsync(service => service.ListAsync(order.ItemId, TestContext.Current.CancellationToken))), _now));
    }

    [Fact]
    public async Task TheConfirmationEmail_CarriesTheDownloadLinks()
    {
        var file = await FileStore().SaveAsync("guide.pdf", new MemoryStream([1]), TestContext.Current.CancellationToken);
        var order = PaidOrder();

        await InSessionAsync(service => service.GrantAsync(order, TestContext.Current.CancellationToken), Product(file, limit: 3, days: 30));

        var section = await InSessionAsync(service => new DownloadEmailSectionProvider(service, new PassThroughLocalizer<DownloadEmailSectionProvider>(), new TestClock(DateTime.UtcNow)).GetSectionAsync(order, TestContext.Current.CancellationToken));

        var link = Assert.Single(section.Links);
        Assert.Equal("guide.pdf", link.Text);
        Assert.StartsWith("https://store.test/downloads/", link.Url, StringComparison.Ordinal);
        Assert.Contains("3 downloads", link.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDigitalFilesRecipeStep_PutsSampleFilesOnProducts_Once()
    {
        var product = new ContentItem { ContentItemId = "ebook", ContentItemVersionId = "ebook-v1", ContentType = "DigitalProduct" };
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(value => value.GetAsync("ebook", It.IsAny<VersionOptions>())).ReturnsAsync(product);

        var step = new CrestApps.OrchardCore.Downloads.Recipes.DigitalFilesStep(contentManager.Object, FileStore());
        var json = new System.Text.Json.Nodes.JsonObject
        {
            ["name"] = "DigitalFiles",
            ["Files"] = new System.Text.Json.Nodes.JsonArray
            {
                new System.Text.Json.Nodes.JsonObject { ["ProductId"] = "ebook", ["FileName"] = "guide.pdf", ["Base64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("%PDF-1.4 sample")) },
                new System.Text.Json.Nodes.JsonObject { ["ProductId"] = "missing", ["FileName"] = "x.pdf", ["Text"] = "x" },
                new System.Text.Json.Nodes.JsonObject { ["ProductId"] = "ebook", ["FileName"] = "broken.pdf", ["Base64"] = "not base64!" },
            },
        };

        var first = new global::OrchardCore.Recipes.Models.RecipeExecutionContext { Name = "DigitalFiles", Step = json };
        await step.ExecuteAsync(first);
        await step.ExecuteAsync(new global::OrchardCore.Recipes.Models.RecipeExecutionContext { Name = "DigitalFiles", Step = (System.Text.Json.Nodes.JsonObject)json.DeepClone() });

        var file = Assert.Single(product.GetOrCreate<DigitalFilesPart>().Files);
        Assert.Equal("guide.pdf", file.FileName);
        Assert.NotNull(await FileStore().FindAsync(file.FileId, TestContext.Current.CancellationToken));
        Assert.Equal(2, first.Errors.Count);
    }

    [Fact]
    public void TheDigitalSample_TurnsOnWhatAFreshStoreNeedsToSellAndDeliver()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var recipe = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(directory.FullName, "src", "Modules", "CrestApps.OrchardCore.Downloads", "Recipes", "digital-downloads-sample.recipe.json")));
        var steps = recipe["steps"].AsArray();
        var features = steps[0]["enable"].AsArray().Select(feature => (string)feature).ToList();

        // Product pages and Add to cart come from Product Listing; the contact step that asks a guest for the email
        // their links go to comes from Customers.
        Assert.Contains("CrestApps.OrchardCore.Products.Listing", features);
        Assert.Contains("CrestApps.OrchardCore.Customers", features);
        Assert.Contains(DownloadsConstants.Feature, features);

        var files = steps.Single(step => (string)step["name"] == "DigitalFiles")["Files"].AsArray();
        Assert.Equal(2, files.Count);
        Assert.All(files, file => Assert.StartsWith("%PDF", Encoding.ASCII.GetString(Convert.FromBase64String((string)file["Base64"]))));

        // The files go on products the recipe has already created.
        var filesAt = steps.IndexOf(steps.Single(step => (string)step["name"] == "DigitalFiles"));
        var content = steps.Take(filesAt).Where(step => string.Equals((string)step["name"], "Content", StringComparison.OrdinalIgnoreCase)).SelectMany(step => step["data"].AsArray()).ToList();
        var ids = content.Select(item => (string)item["ContentItemId"]).ToHashSet();
        Assert.All(files, file => Assert.Contains((string)file["ProductId"], ids));
        Assert.Contains(content, item => (string)item["ContentType"] == "ProductListing");

        Assert.Contains(steps, step => (string)step["name"] == "Coupons");
    }

    private async Task<bool> DownloadAsync(string token)
        => await InSessionAsync(async service =>
        {
            var (entitlement, problem) = await service.ResolveAsync(token, TestContext.Current.CancellationToken);

            return problem == DownloadProblem.None && await service.RecordDownloadAsync(entitlement, TestContext.Current.CancellationToken);
        });

    private async Task<T> InSessionAsync<T>(Func<DownloadService, Task<T>> work, ContentItem product = null)
    {
        await using var session = _store.CreateSession();

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(value => value.GetVersionAsync(It.IsAny<string>())).ReturnsAsync(product);

        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<DownloadSettings>()).Returns(new DownloadSettings());
        site.SetupGet(value => value.BaseUrl).Returns("https://store.test");

        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(() => _clockNow);

        var service = new DownloadService(session, contentManager.Object, siteService.Object, _protection, new HttpContextAccessor(), Mock.Of<LinkGenerator>(), _lock, clock.Object);
        var result = await work(service);

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }

    private DigitalFileStore FileStore(params IFileEventHandler[] handlers)
        => new(new FileSystemStore(_folder, NullLogger<FileSystemStore>.Instance), new FileCreationService(handlers));

    private static ContentItem Product(StoredDigitalFile file, int limit, int days = 30)
    {
        var product = new ContentItem { ContentItemId = "ebook", ContentItemVersionId = "ebook-v1", ContentType = "Product" };

        product.Alter<DigitalFilesPart>(part =>
        {
            part.Files = [new DigitalFile { FileId = file.FileId, FileName = file.FileName, Size = file.Size }];
            part.DownloadLimit = limit;
            part.LinkLifetimeDays = days;
        });

        return product;
    }

    private static Order PaidOrder()
        => new()
        {
            ItemId = "order-1",
            OrderNumber = "261018-EBOOK1",
            Status = OrderStatus.Paid,
            Currency = "USD",
            Total = 20m,
            Lines = [new OrderLine { LineId = "line-1", ContentItemId = "ebook", ContentItemVersionId = "ebook-v1", Title = "Field Guide", Quantity = 1, UnitPrice = 20m, LineTotal = 20m }],
        };

    private sealed class PassThroughLocalizer<T> : Microsoft.Extensions.Localization.IStringLocalizer<T>
    {
        public Microsoft.Extensions.Localization.LocalizedString this[string name]
            => new(name, name);

        public Microsoft.Extensions.Localization.LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<Microsoft.Extensions.Localization.LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}

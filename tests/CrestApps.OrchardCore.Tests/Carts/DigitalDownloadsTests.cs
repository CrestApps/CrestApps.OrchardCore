using System.Text;
using CrestApps.OrchardCore.Downloads;
using CrestApps.OrchardCore.Downloads.Handlers;
using CrestApps.OrchardCore.Downloads.Indexes;
using CrestApps.OrchardCore.Downloads.Migrations;
using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Downloads.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.FileStorage.FileSystem;
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
        await InSessionAsync(async service => { await new DownloadOrderEventHandler(service).RefundedAsync(new OrderRefundedContext(order, order.Refunds[0]), TestContext.Current.CancellationToken); return 0; });
        Assert.Null(Assert.Single(await InSessionAsync(service => service.ListAsync(order.ItemId, TestContext.Current.CancellationToken))).RevokedUtc);

        order.Refunds.Add(new OrderRefund { RefundId = "r2", Amount = 15m });
        await InSessionAsync(async service => { await new DownloadOrderEventHandler(service).RefundedAsync(new OrderRefundedContext(order, order.Refunds[1]), TestContext.Current.CancellationToken); return 0; });

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

        var section = await InSessionAsync(service => new DownloadEmailSectionProvider(service, new PassThroughLocalizer<DownloadEmailSectionProvider>()).GetSectionAsync(order, TestContext.Current.CancellationToken));

        var link = Assert.Single(section.Links);
        Assert.Equal("guide.pdf", link.Text);
        Assert.StartsWith("https://store.test/downloads/", link.Url, StringComparison.Ordinal);
        Assert.Contains("3 downloads", link.Note, StringComparison.Ordinal);
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

    private DigitalFileStore FileStore()
        => new(new FileSystemStore(_folder, NullLogger<FileSystemStore>.Instance));

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

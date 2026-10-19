using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Migrations;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
using OrchardCore.Modules;
using OrchardCore.Settings;
using Xunit;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Inventory;

/// <summary>
/// A shopper asks to hear when a sold-out product is back, once per product, and is emailed once when it is.
/// </summary>
public sealed class StockAlertServiceTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-stock-alerts-{Guid.NewGuid():N}.db");
    private readonly List<MailMessage> _sent = [];
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new StockItemIndexProvider(), new StockMovementIndexProvider(), new StockReservationIndexProvider(), new StockAlertIndexProvider()], InventoryConstants.CollectionName);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);
        await _store.InitializeCollectionAsync(InventoryConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new InventoryMigrations(Mock.Of<IContentDefinitionManager>()) { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
        await new StockAlertMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ASoldOutProduct_TakesOneRequestPerEmail_AndAnInStockOneTakesNone()
    {
        await AddItemAsync("cap", onHand: 0);
        await AddItemAsync("mug", onHand: 3);

        Assert.False((await SubscribeAsync("cap", "not-an-email")).Succeeded);
        Assert.True((await SubscribeAsync("cap", "Sam@Example.test")).Succeeded);
        Assert.True((await SubscribeAsync("cap", "sam@example.test")).Succeeded);
        Assert.False((await SubscribeAsync("mug", "sam@example.test")).Succeeded);
        Assert.False((await SubscribeAsync("nothing", "sam@example.test")).Succeeded);

        await using var session = _store.CreateSession();
        Assert.Equal(1, await CreateService(session).CountWaitingAsync((await new StockItemStore(session).FindAsync("cap", null, cancellationToken: TestContext.Current.CancellationToken)).ItemId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BackInStock_EmailsEveryoneWaiting_Once()
    {
        var item = await AddItemAsync("cap", onHand: 0);

        await SubscribeAsync("cap", "sam@example.test");
        await SubscribeAsync("cap", "alex@example.test");

        await using (var session = _store.CreateSession())
        {
            Assert.Equal(2, await CreateService(session).NotifyAsync(item, TestContext.Current.CancellationToken));
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            Assert.Equal(0, await CreateService(session).NotifyAsync(item, TestContext.Current.CancellationToken));
        }

        Assert.Equal(["sam@example.test", "alex@example.test"], _sent.Select(message => message.To));
        Assert.All(_sent, message => Assert.Contains("cap", message.Subject));
    }

    private async Task<StockAlertResult> SubscribeAsync(string contentItemId, string email)
    {
        await using var session = _store.CreateSession();
        var result = await CreateService(session).SubscribeAsync(contentItemId, null, email, userId: null, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }

    private async Task<StockItem> AddItemAsync(string contentItemId, int onHand)
    {
        var item = new StockItem { ItemId = contentItemId + "-item", ContentItemId = contentItemId, Title = contentItemId, OnHand = onHand };

        await using var session = _store.CreateSession();
        await new StockItemStore(session).CreateAsync(item, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return item;
    }

    private StockAlertService CreateService(ISession session)
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        var email = new Mock<IEmailService>();
        email.Setup(service => service.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((MailMessage message, string _, CancellationToken _) => _sent.Add(message))
            .ReturnsAsync(Result.Success());

        var site = new Mock<ISite>();
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var services = new ServiceCollection()
            .AddSingleton(email.Object)
            .AddSingleton(siteService.Object)
            .BuildServiceProvider();

        return new StockAlertService(
            session,
            new StockItemStore(session),
            Mock.Of<IContentManager>(),
            clock.Object,
            services,
            NullLogger<StockAlertService>.Instance,
            new PassThroughStringLocalizer<StockAlertService>());
    }
}

using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Fulfillment;
using CrestApps.OrchardCore.Fulfillment.Core.Indexes;
using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Fulfillment.Models;
using CrestApps.OrchardCore.Fulfillment.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Shipping.Indexes;
using CrestApps.OrchardCore.Shipping.Migrations;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Fulfillment;

/// <summary>
/// A SQLite store with the fulfillment requests and the real Shipping shipment store and service, so what a partner is
/// sent, and the shipment that reserves it, are checked as stored.
/// </summary>
internal sealed class FulfillmentTestHarness : IAsyncDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-fulfillment-{Guid.NewGuid():N}.db");

    public IStore Store { get; private set; }

    public Order Order { get; set; } = PaidOrder();

    public DateTime Now { get; set; } = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    public List<FakeProvider> Providers { get; } = [];

    public List<FulfillmentRequest> Attention { get; } = [];

    public bool LockAvailable { get; set; } = true;

    public TimeSpan SubmitTimeout { get; set; } = FulfillmentConstants.SubmitTimeout;

    public static CancellationToken Token
        => TestContext.Current.CancellationToken;

    public async Task InitializeAsync()
    {
        Store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        Store.RegisterIndexes([new FulfillmentRequestIndexProvider()], FulfillmentConstants.CollectionName);
        Store.RegisterIndexes([new ShipmentIndexProvider()], ShippingConstants.CollectionName);
        await Store.InitializeAsync(Token);
        await Store.InitializeCollectionAsync(FulfillmentConstants.CollectionName, Token);
        await Store.InitializeCollectionAsync(ShippingConstants.CollectionName, Token);

        await using var session = Store.CreateSession();
        var transaction = await session.BeginTransactionAsync(Token);
        var schemaBuilder = new SchemaBuilder(Store.Configuration, transaction);

        await FulfillmentIndexSchema.CreateAsync(schemaBuilder);
        await new ShippingMigrations(Mock.Of<IContentDefinitionManager>()) { SchemaBuilder = schemaBuilder }.CreateAsync();
        await transaction.CommitAsync(Token);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(Store, _path);

        return ValueTask.CompletedTask;
    }

    public FakeProvider AddProvider(string name, params string[] claimedLines)
    {
        var provider = new FakeProvider(name, claimedLines);

        Providers.Add(provider);

        return provider;
    }

    public FulfillmentSender Sender(ISession session)
    {
        var clock = new Mock<IClock>();
        var distributedLock = new Mock<IDistributedLock>();
        var shipmentStore = new ShipmentStore(session);
        var shipments = new DefaultShipmentService(shipmentStore, Orders(), Mock.Of<ISiteService>(), [], clock.Object);
        var handler = new Mock<IFulfillmentRequestHandler>();

        clock.Setup(c => c.UtcNow).Returns(() => Now);
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(() => (Mock.Of<ILocker>(), LockAvailable));
        handler
            .Setup(h => h.NeedsAttentionAsync(It.IsAny<FulfillmentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<FulfillmentRequest, CancellationToken>((request, _) => Attention.Add(request))
            .Returns(Task.CompletedTask);

        return new FulfillmentSender(
            new FulfillmentRequestStore(session),
            Orders(),
            Providers,
            new FulfillmentOrderBuilder(shipments),
            new FulfillmentLineAssigner(Providers, NullLogger<FulfillmentLineAssigner>.Instance),
            shipments,
            shipmentStore,
            [handler.Object],
            clock.Object,
            distributedLock.Object,
            NullLogger<FulfillmentSender>.Instance)
        {
            SubmitTimeout = SubmitTimeout,
        };
    }

    public DefaultShipmentService Shipments(ISession session)
        => new(new ShipmentStore(session), Orders(), Mock.Of<ISiteService>(), [], Mock.Of<IClock>(c => c.UtcNow == Now));

    public async Task<IReadOnlyList<FulfillmentRequest>> RecordAsync()
    {
        await using var session = Store.CreateSession();

        return await Sender(session).RecordAsync(Order, lockAndCommit: true, Token);
    }

    public async Task<FulfillmentSendResult> SendAsync(string providerName)
    {
        await using var session = Store.CreateSession();
        var requests = await new FulfillmentRequestStore(session).ListByOrderAsync(Order.ItemId, Token);
        var request = Assert.Single(requests, item => item.ProviderName == providerName);
        var result = await Sender(session).SendAsync(request, Token);

        await session.SaveChangesAsync(Token);

        return result;
    }

    public async Task<FulfillmentSendResult> SendByIdAsync(string requestId)
    {
        await using var session = Store.CreateSession();
        var request = await new FulfillmentRequestStore(session).FindByIdAsync(requestId, Token);
        var result = await Sender(session).SendAsync(request, Token);

        await session.SaveChangesAsync(Token);

        return result;
    }

    public async Task<int> ProcessDueAsync()
    {
        await using var session = Store.CreateSession();
        var processed = await Sender(session).ProcessDueAsync(10, Token);

        await session.SaveChangesAsync(Token);

        return processed;
    }

    public async Task<IReadOnlyList<FulfillmentRequest>> LoadRequestsAsync()
    {
        await using var session = Store.CreateSession();

        return await new FulfillmentRequestStore(session).ListByOrderAsync(Order.ItemId, Token);
    }

    public async Task<IReadOnlyList<Shipment>> LoadShipmentsAsync()
    {
        await using var session = Store.CreateSession();

        return await new ShipmentStore(session).ListByOrderAsync(Order.ItemId, Token);
    }

    public async Task SaveShipmentAsync(Shipment shipment)
    {
        await using var session = Store.CreateSession();

        await new ShipmentStore(session).CreateAsync(shipment, Token);
        await session.SaveChangesAsync(Token);
    }

    public FulfillmentRequest NewRequest(string itemId, string providerName)
        => new()
        {
            ItemId = itemId,
            OrderId = Order.ItemId,
            OrderNumber = Order.OrderNumber,
            ProviderName = providerName,
            Status = FulfillmentRequestStatus.Pending,
            NextAttemptUtc = Now,
            CreatedUtc = Now,
            ModifiedUtc = Now,
        };

    public static Order PaidOrder()
        => new()
        {
            ItemId = "order-1",
            OrderNumber = "261117-ABC",
            Status = OrderStatus.Paid,
            CustomerEmail = "test@example.com",
            ShippingAddress = new Address { Name = "Test Customer", AddressLine1 = "123 Test Street", City = "Cincinnati", Region = "OH", PostalCode = "45202", Country = "US" },
            Lines =
            [
                new OrderLine { LineId = "l1", ContentItemId = "p1", VariantId = "v1", Title = "Tee", Quantity = 2, ProductType = ProductType.Good },
                new OrderLine { LineId = "l2", ContentItemId = "p2", VariantId = "v2", Title = "Mug", Quantity = 1, ProductType = ProductType.Good },
            ],
        };

    private IOrderStore Orders()
    {
        var orders = new Mock<IOrderStore>();

        // The same order every time, so a change Shipping makes to it, such as its fulfillment status, can be seen.
        orders.Setup(o => o.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Order);

        return orders.Object;
    }

    /// <summary>
    /// A partner that makes the lines it is told to, and records what it was sent.
    /// </summary>
    internal sealed class FakeProvider : IFulfillmentProvider
    {
        private readonly string[] _claimedLines;

        public FakeProvider(string name, string[] claimedLines)
        {
            Name = name;
            _claimedLines = claimedLines;
        }

        public string Name { get; }

        public LocalizedString DisplayName => new(Name, Name);

        public int Calls { get; private set; }

        public List<FulfillmentOrder> Received { get; } = [];

        public Func<CancellationToken, Task<FulfillmentSubmitResult>> OnSubmit { get; set; } = _ => Task.FromResult(FulfillmentSubmitResult.Sent("partner-1", "Accepted."));

        public Func<FulfillmentOrder, IReadOnlyList<FulfillmentLineClaim>> OnClaim { get; set; }

        public Task<bool> IsConnectedAsync()
            => Task.FromResult(true);

        public Task<FulfillmentConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(FulfillmentConnectionResult.Success("Connected."));

        public Task<IReadOnlyList<FulfillmentLineClaim>> ClaimLinesAsync(FulfillmentOrder order, CancellationToken cancellationToken = default)
        {
            if (OnClaim is not null)
            {
                return Task.FromResult(OnClaim(order));
            }

            IReadOnlyList<FulfillmentLineClaim> claims = [.. order.Lines
                .Where(line => _claimedLines.Contains(line.LineId))
                .Select(line => new FulfillmentLineClaim(line.LineId, line.Quantity))];

            return Task.FromResult(claims);
        }

        public Task<FulfillmentSubmitResult> SubmitAsync(FulfillmentOrder order, CancellationToken cancellationToken = default)
        {
            Calls++;
            Received.Add(order);

            return OnSubmit(cancellationToken);
        }
    }
}

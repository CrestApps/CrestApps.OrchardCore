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
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Fulfillment;

/// <summary>
/// A paid order is sent to a partner once: the request is stored as being sent before the partner is called, a second
/// record of the same order is never sent, an unclear outcome goes to a person and is not sent again, a busy partner
/// is tried again later, and a send that was interrupted is flagged.
/// </summary>
public sealed class FulfillmentSenderTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-fulfillment-{Guid.NewGuid():N}.db");
    private readonly FakeProvider _provider = new();
    private DateTime _now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
    private IStore _store;
    private Order _order;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new FulfillmentRequestIndexProvider()]);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);
        await _store.InitializeCollectionAsync(FulfillmentConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await FulfillmentIndexSchema.CreateAsync(new SchemaBuilder(_store.Configuration, transaction));
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        _order = PaidOrder();
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task SendAsync_PartnerAccepts_SendsOnceAndRecordsTheOrder()
    {
        // Arrange
        await RecordAsync();

        // Act
        var first = await SendOnlyRequestAsync();
        var second = await SendOnlyRequestAsync();

        // Assert
        Assert.Equal(FulfillmentRequestStatus.Sent, first.Status);
        Assert.Equal("partner-1", first.ExternalOrderId);
        Assert.Equal(FulfillmentRequestStatus.Sent, second.Status);
        Assert.Equal(1, _provider.Calls);
    }

    [Fact]
    public async Task SendAsync_StoresSendingBeforeThePartnerIsCalled()
    {
        // Arrange
        await RecordAsync();
        FulfillmentRequestStatus? statusSeenByPartner = null;

        _provider.OnSubmit = async () =>
        {
            await using var other = _store.CreateSession();
            var stored = await new FulfillmentRequestStore(other).ListByOrderAsync(_order.ItemId, TestContext.Current.CancellationToken);

            statusSeenByPartner = Assert.Single(stored).Status;

            return FulfillmentSubmitResult.Sent("partner-1", "Sent.");
        };

        // Act
        await SendOnlyRequestAsync();

        // Assert
        Assert.Equal(FulfillmentRequestStatus.Sending, statusSeenByPartner);
    }

    [Fact]
    public async Task SendAsync_TwoRecordsOfTheSameOrder_OnlyOneIsSent()
    {
        // Arrange
        await using (var session = _store.CreateSession())
        {
            var store = new FulfillmentRequestStore(session);

            await store.CreateAsync(NewRequest("request-a"), TestContext.Current.CancellationToken);
            await store.CreateAsync(NewRequest("request-b"), TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var a = await SendAsync("request-a");
        var b = await SendAsync("request-b");

        // Assert
        Assert.Equal(FulfillmentRequestStatus.Sent, a.Status);
        Assert.Equal(FulfillmentRequestStatus.NothingToSend, b.Status);
        Assert.Equal(1, _provider.Calls);
    }

    [Fact]
    public async Task SendAsync_PartnerFailsUnexpectedly_AsksAPersonAndIsNotSentAgain()
    {
        // Arrange
        await RecordAsync();
        _provider.OnSubmit = () => throw new InvalidOperationException("Connection reset after the request was written.");

        // Act
        var result = await SendOnlyRequestAsync();
        var processed = await ProcessDueAsync();

        // Assert
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, result.Status);
        Assert.Contains("not known whether", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, processed);
        Assert.Equal(1, _provider.Calls);
    }

    [Fact]
    public async Task SendAsync_PartnerBusy_WaitsAndTriesAgainLater()
    {
        // Arrange
        await RecordAsync();
        _provider.OnSubmit = () => Task.FromResult(FulfillmentSubmitResult.RetryLater("Busy."));

        // Act
        var result = await SendOnlyRequestAsync();
        var tooSoon = await ProcessDueAsync();

        _now = _now.AddMinutes(5);
        _provider.OnSubmit = () => Task.FromResult(FulfillmentSubmitResult.Sent("partner-2", "Sent."));

        var later = await ProcessDueAsync();
        var final = await LoadOnlyRequestAsync();

        // Assert
        Assert.Equal(FulfillmentRequestStatus.Pending, result.Status);
        Assert.Equal(0, tooSoon);
        Assert.Equal(1, later);
        Assert.Equal(FulfillmentRequestStatus.Sent, final.Status);
        Assert.Equal(2, _provider.Calls);
    }

    [Fact]
    public async Task SendAsync_OrderNoLongerPaid_AsksAPersonWithoutCallingThePartner()
    {
        // Arrange
        await RecordAsync();
        _order.Status = OrderStatus.Canceled;

        // Act
        var result = await SendOnlyRequestAsync();

        // Assert
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, result.Status);
        Assert.Equal(0, _provider.Calls);
    }

    [Fact]
    public async Task FlagStuckAsync_SendInterrupted_AsksAPerson()
    {
        // Arrange
        await using (var session = _store.CreateSession())
        {
            var request = NewRequest("request-stuck");

            request.Status = FulfillmentRequestStatus.Sending;
            request.ModifiedUtc = _now.AddMinutes(-20);

            await new FulfillmentRequestStore(session).CreateAsync(request, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        int flagged;

        await using (var session = _store.CreateSession())
        {
            flagged = await Sender(session).FlagStuckAsync(TestContext.Current.CancellationToken);
        }

        var result = await LoadOnlyRequestAsync();

        // Assert
        Assert.Equal(1, flagged);
        Assert.Equal(FulfillmentRequestStatus.NeedsAttention, result.Status);
        Assert.Equal(0, _provider.Calls);
    }

    private async Task RecordAsync()
    {
        await using var session = _store.CreateSession();

        await Sender(session).RecordAsync(_order, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<FulfillmentRequest> SendOnlyRequestAsync()
    {
        await using var session = _store.CreateSession();
        var request = Assert.Single(await new FulfillmentRequestStore(session).ListByOrderAsync(_order.ItemId, TestContext.Current.CancellationToken));
        var result = await Sender(session).SendAsync(request, TestContext.Current.CancellationToken);

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }

    private async Task<FulfillmentRequest> SendAsync(string requestId)
    {
        await using var session = _store.CreateSession();
        var request = await new FulfillmentRequestStore(session).FindByIdAsync(requestId, TestContext.Current.CancellationToken);
        var result = await Sender(session).SendAsync(request, TestContext.Current.CancellationToken);

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }

    private async Task<int> ProcessDueAsync()
    {
        await using var session = _store.CreateSession();
        var processed = await Sender(session).ProcessDueAsync(10, TestContext.Current.CancellationToken);

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return processed;
    }

    private async Task<FulfillmentRequest> LoadOnlyRequestAsync()
    {
        await using var session = _store.CreateSession();

        return Assert.Single(await new FulfillmentRequestStore(session).ListByOrderAsync(_order.ItemId, TestContext.Current.CancellationToken));
    }

    private FulfillmentSender Sender(ISession session)
    {
        var orders = new Mock<IOrderStore>();
        var clock = new Mock<IClock>();
        var distributedLock = new Mock<IDistributedLock>();

        orders.Setup(o => o.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _order);
        clock.Setup(c => c.UtcNow).Returns(() => _now);
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((Mock.Of<ILocker>(), true));

        return new FulfillmentSender(new FulfillmentRequestStore(session), orders.Object, [_provider], clock.Object, distributedLock.Object, NullLogger<FulfillmentSender>.Instance);
    }

    private FulfillmentRequest NewRequest(string itemId)
        => new()
        {
            ItemId = itemId,
            OrderId = _order.ItemId,
            OrderNumber = _order.OrderNumber,
            ProviderName = FakeProvider.ProviderName,
            Status = FulfillmentRequestStatus.Pending,
            NextAttemptUtc = _now,
            CreatedUtc = _now,
            ModifiedUtc = _now,
        };

    private static Order PaidOrder()
        => new()
        {
            ItemId = "order-1",
            OrderNumber = "261117-ABC",
            Status = OrderStatus.Paid,
            CustomerEmail = "test@example.com",
            ShippingAddress = new Address { Name = "Test Customer", AddressLine1 = "123 Test Street", City = "Cincinnati", Region = "OH", PostalCode = "45202", Country = "US" },
            Lines = [new OrderLine { LineId = "l1", ContentItemId = "p1", VariantId = "v1", Title = "Tee", Quantity = 1, ProductType = ProductType.Good }],
        };

    private sealed class FakeProvider : IFulfillmentProvider
    {
        public const string ProviderName = "Fake";

        public int Calls { get; private set; }

        public Func<Task<FulfillmentSubmitResult>> OnSubmit { get; set; } = () => Task.FromResult(FulfillmentSubmitResult.Sent("partner-1", "Sent."));

        public string Name => ProviderName;

        public LocalizedString DisplayName => new(ProviderName, ProviderName);

        public Task<bool> IsConnectedAsync()
            => Task.FromResult(true);

        public Task<FulfillmentConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(FulfillmentConnectionResult.Success("Connected."));

        public Task<FulfillmentSubmitResult> SubmitAsync(FulfillmentOrder order, CancellationToken cancellationToken = default)
        {
            Calls++;

            return OnSubmit();
        }
    }
}

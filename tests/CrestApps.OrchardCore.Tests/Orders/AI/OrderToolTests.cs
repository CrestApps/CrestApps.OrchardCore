using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Orders;
using CrestApps.OrchardCore.Orders.AI;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using CrestApps.OrchardCore.Tests.Agent.Indexing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Orders.AI;

/// <summary>
/// The order tools need Manage orders, build the orders list's query, return only the listed fields of an order (no
/// phone, pay link or tender reference), refund only a stated amount in the order's currency with an idempotency key,
/// cancel without refunding, and send emails only when the order page would offer it.
/// </summary>
public sealed class OrderToolTests
{
    private readonly Order _order = new()
    {
        ItemId = "order-1",
        OrderNumber = "1042",
        Status = OrderStatus.Paid,
        Currency = "USD",
        Subtotal = 50m,
        Total = 55m,
        CustomerName = "Jane Buyer",
        CustomerEmail = "jane@example.com",
        CustomerPhone = "+15550001111",
        CheckoutSessionId = "session-1",
        BillingAddress = new Address { Name = "Jane Buyer", AddressLine1 = "1 Main St", City = "Springfield", Country = "US", Phone = "+15550002222" },
        Tenders = [new OrderTender { ProviderKey = "gift-card", Reference = "GIFT-SECRET-CODE", Description = "Gift card", Amount = 5m }],
        Lines = [new OrderLine { LineId = "line-1", ContentItemId = "mug", Title = "Classic mug", Quantity = 2, UnitPrice = 25m, LineTotal = 50m }],
        Notes = [new OrderNote { Text = "Gift wrap", AuthorName = "admin", CreatedUtc = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc) }],
    };

    private readonly Mock<IOrderStore> _orders = new();
    private readonly Mock<IOrderOperationsService> _operations = new();
    private readonly Mock<IOrderService> _orderService = new();
    private readonly Mock<ICommerceEmailSender> _email = new();
    private readonly MemoryDistributedCache _cache = new(Options.Create(new MemoryDistributedCacheOptions()));
    private OrderQuery _query;

    public OrderToolTests()
    {
        _orders.Setup(store => store.FindByIdAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(_order);
        _orders.Setup(store => store.FindByNumberAsync("1042", It.IsAny<CancellationToken>())).ReturnsAsync(_order);
        _orders.Setup(store => store.PageAsync(It.IsAny<OrderQuery>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback((OrderQuery query, int _, int _, CancellationToken _) => _query = query)
            .ReturnsAsync((1, (IReadOnlyList<Order>)[_order]));
        _operations.Setup(operations => operations.GetMoneyAsync(_order, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderMoney { Currency = "USD", Refundable = 55m });
        _operations.Setup(operations => operations.RefundAsync(_order, It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order _, decimal? amount, string _, bool _, string _, bool _, CancellationToken _) => new OrderOperationResult { Refunded = amount ?? 0m });
        _operations.Setup(operations => operations.CancelAsync(_order, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderOperationResult());
        _operations.Setup(operations => operations.ResendConfirmationAsync(_order, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _email.Setup(email => email.CanSend).Returns(true);
    }

    public static TheoryData<string> ToolNames => new()
    {
        ListOrdersTool.TheName,
        GetOrderTool.TheName,
        AddOrderNoteTool.TheName,
        RefundOrderTool.TheName,
        CancelOrderTool.TheName,
        ResendOrderConfirmationTool.TheName,
        SaveDraftOrderTool.TheName,
        SendOrderInvoiceTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName));

        // Assert
        Assert.Equal(OrdersAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_operations.Invocations);
        Assert.Empty(_orderService.Invocations);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutManageOrders(string toolName)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != OrderPermissions.ManageOrders.Name);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(OrdersAIToolBase.NotAllowedMessage, result);
        Assert.Equal(OrderPermissions.ManageOrders.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_orders.Invocations);
        Assert.Empty(_operations.Invocations);
    }

    [Fact]
    public async Task ListOrders_BuildsTheOrdersListQuery_WithTheDaysInTheSitesTimeZone()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ListOrdersTool.TheName, Build(), new()
        {
            ["status"] = "paid",
            ["channel"] = "online",
            ["placedFrom"] = "2026-11-01",
            ["placedTo"] = "2026-11-02",
        }));
        var wrong = await InvokeAsync(ListOrdersTool.TheName, Build(), new() { ["status"] = "shipped" });

        // Assert
        Assert.Equal(new[] { OrderStatus.Paid }, _query.Statuses);
        Assert.Equal(OrderChannels.Online, _query.Channel);
        Assert.Equal(new DateTime(2026, 11, 1), _query.CreatedOnOrAfter);
        Assert.Equal(new DateTime(2026, 11, 3), _query.CreatedBefore);
        Assert.Equal("1042", Assert.Single(json.GetProperty("items").EnumerateArray()).GetProperty("number").GetString());
        Assert.StartsWith("Nothing was listed: status must be", wrong, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOrder_ReturnsTheListedFieldsOnly()
    {
        // Act
        var result = await InvokeAsync(GetOrderTool.TheName, Build(), new() { ["orderNumber"] = "1042" });
        var missing = await InvokeAsync(GetOrderTool.TheName, Build(), new() { ["orderNumber"] = "9999" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("order-1", json.GetProperty("orderId").GetString());
        Assert.Equal(55m, json.GetProperty("refundable").GetDecimal());
        Assert.Equal("Gift wrap", Assert.Single(json.GetProperty("notes").EnumerateArray()).GetProperty("text").GetString());
        Assert.False(json.GetProperty("hasPayLink").GetBoolean());
        Assert.DoesNotContain("+1555", result, StringComparison.Ordinal);
        Assert.DoesNotContain("GIFT-SECRET-CODE", result, StringComparison.Ordinal);
        Assert.DoesNotContain("session-1", result, StringComparison.Ordinal);
        Assert.Contains("no order has the number '9999'", missing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddOrderNote_AddsAStaffNoteUnderTheCallersName()
    {
        // Act
        var result = await InvokeAsync(AddOrderNoteTool.TheName, Build(), new() { ["orderId"] = "order-1", ["text"] = "Called the buyer." });
        var empty = await InvokeAsync(AddOrderNoteTool.TheName, Build(), new() { ["orderId"] = "order-1", ["text"] = " " });

        // Assert
        Assert.Contains("The note was added", result, StringComparison.Ordinal);
        Assert.Contains("write the note first", empty, StringComparison.Ordinal);
        _orderService.Verify(service => service.AddNoteAsync(_order, "Called the buyer.", OrderNoteKind.Note, AgentToolTestContext.UserName, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(20.0, "EUR", false, "was paid in USD")]
    [InlineData(20.005, "USD", false, "more decimal places")]
    [InlineData(55.0, "USD", false, "Set refundAll to true")]
    [InlineData(60.0, "USD", false, "only 55.00 USD can still be refunded")]
    [InlineData(20.0, "USD", true, "everything still refundable")]
    public async Task RefundOrder_RefusesAnAmountTheRulesDoNotAllow_AndRefundsNothing(double amount, string currency, bool refundAll, string expected)
    {
        // Act
        var result = await InvokeAsync(RefundOrderTool.TheName, Build(), new()
        {
            ["orderId"] = "order-1",
            ["amount"] = (decimal)amount,
            ["currency"] = currency,
            ["refundAll"] = refundAll,
            ["idempotencyKey"] = "refund-1",
        });

        // Assert
        Assert.StartsWith("Nothing was refunded", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        VerifyNoRefund();
    }

    [Fact]
    public async Task RefundOrder_RefundsOnce_AndRefusesTheSameKeyAgain()
    {
        // Arrange
        var arguments = new Dictionary<string, object>
        {
            ["orderId"] = "order-1",
            ["amount"] = 20m,
            ["currency"] = "usd",
            ["reason"] = "Box arrived damaged",
            ["idempotencyKey"] = "refund-1042-damaged",
        };

        // Act
        var first = AgentToolTestContext.ReadJson(await InvokeAsync(RefundOrderTool.TheName, Build(), arguments));
        var again = await InvokeAsync(RefundOrderTool.TheName, Build(), arguments);
        var otherRefund = await InvokeAsync(RefundOrderTool.TheName, Build(), new Dictionary<string, object>(arguments) { ["amount"] = 10m });
        var all = AgentToolTestContext.ReadJson(await InvokeAsync(RefundOrderTool.TheName, Build(), new Dictionary<string, object>(arguments) { ["amount"] = 55m, ["refundAll"] = true, ["idempotencyKey"] = "refund-1042-all" }));

        // Assert
        Assert.Equal(20m, first.GetProperty("refunded").GetDecimal());
        Assert.StartsWith("Nothing was refunded again", again, StringComparison.Ordinal);
        Assert.Contains("used for a different refund", otherRefund, StringComparison.Ordinal);
        Assert.Equal(55m, all.GetProperty("refunded").GetDecimal());
        _operations.Verify(operations => operations.RefundAsync(_order, 20m, "Box arrived damaged", false, AgentToolTestContext.UserName, false, It.IsAny<CancellationToken>()), Times.Once);
        _operations.Verify(operations => operations.RefundAsync(_order, 55m, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefundOrder_NeedsAnIdempotencyKey()
    {
        // Act
        var result = await InvokeAsync(RefundOrderTool.TheName, Build(), new() { ["orderId"] = "order-1", ["amount"] = 20m, ["currency"] = "USD" });

        // Assert
        Assert.Contains("idempotencyKey", result, StringComparison.Ordinal);
        VerifyNoRefund();
    }

    [Fact]
    public async Task CancelOrder_CancelsWithoutRefunding_AndSaysWhatIsStillRefundable()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(CancelOrderTool.TheName, Build(), new() { ["orderId"] = "order-1", ["note"] = "Buyer asked", ["restock"] = true }));

        // Assert
        Assert.Contains("refundOrder", json.GetProperty("message").GetString(), StringComparison.Ordinal);
        _operations.Verify(operations => operations.CancelAsync(_order, "Buyer asked", false, true, AgentToolTestContext.UserName, It.IsAny<CancellationToken>()), Times.Once);
        VerifyNoRefund();
    }

    [Fact]
    public async Task ResendOrderConfirmation_SendsForAPaidOrder_AndNotForAnUnpaidOne()
    {
        // Act
        var sent = AgentToolTestContext.ReadJson(await InvokeAsync(ResendOrderConfirmationTool.TheName, Build(), new() { ["orderId"] = "order-1" }));
        _order.Status = OrderStatus.AwaitingPayment;
        var unpaid = await InvokeAsync(ResendOrderConfirmationTool.TheName, Build(), new() { ["orderId"] = "order-1" });

        // Assert
        Assert.Equal("jane@example.com", sent.GetProperty("sentTo").GetString());
        Assert.Contains("is not paid", unpaid, StringComparison.Ordinal);
        _operations.Verify(operations => operations.ResendConfirmationAsync(_order, AgentToolTestContext.UserName, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendOrderInvoice_RefusesAnOrderTheStoreDidNotPutTogether()
    {
        // Act
        var result = await InvokeAsync(SendOrderInvoiceTool.TheName, Build(), new() { ["orderId"] = "order-1" });

        // Assert
        Assert.Contains("has no invoice to send", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""[{ "productId": "mug", "quantity": 0 }]""", "quantity from 1 to 10000")]
    [InlineData("""[{ "productId": "mug", "quantity": 1, "unitPrice": -1 }]""", "quoted unit price")]
    [InlineData("""[{ "quantity": 1 }]""", "needs a productId")]
    public async Task SaveDraftOrder_RefusesLinesOutsideTheEditorsLimits(string lines, string expected)
    {
        // Act
        var result = await InvokeAsync(SaveDraftOrderTool.TheName, Build(), new()
        {
            ["customerEmail"] = "jane@example.com",
            ["lines"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(lines),
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveDraftOrder_RefusesAnOrderThatIsNotADraft()
    {
        // Act
        var result = await InvokeAsync(SaveDraftOrderTool.TheName, Build(), new() { ["orderId"] = "order-1", ["note"] = "Changed" });

        // Assert
        Assert.Contains("is not a draft the store put together", result, StringComparison.Ordinal);
    }

    private void VerifyNoRefund()
        => _operations.Verify(operations => operations.RefundAsync(It.IsAny<Order>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            ListOrdersTool.TheName => [],
            AddOrderNoteTool.TheName => new() { ["orderId"] = "order-1", ["text"] = "Note" },
            RefundOrderTool.TheName => new() { ["orderId"] = "order-1", ["amount"] = 20m, ["currency"] = "USD", ["idempotencyKey"] = "refund-1" },
            SaveDraftOrderTool.TheName => new() { ["customerEmail"] = "jane@example.com", ["priceOnly"] = true },
            _ => new() { ["orderId"] = "order-1" },
        };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(_orders.Object);
        services.AddSingleton(_operations.Object);
        services.AddSingleton(_orderService.Object);
        services.AddSingleton(Mock.Of<YesSqlSession>());
        services.AddSingleton<OrdersToolScope>();
        services.AddSingleton<IDistributedCache>(_cache);
        services.AddSingleton<IDistributedLock>(new FakeDistributedLock());
        services.AddSingleton(new OrderConfirmationSender(
            _orders.Object,
            new OrderConfirmationEmailBuilder(new ServiceCollection().BuildServiceProvider(), new PassThroughStringLocalizer<OrderConfirmationEmailBuilder>()),
            Mock.Of<IOrderLinkService>(),
            _email.Object,
            Mock.Of<IClock>()));

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListOrdersTool.TheName => new ListOrdersTool(),
            GetOrderTool.TheName => new GetOrderTool(),
            AddOrderNoteTool.TheName => new AddOrderNoteTool(),
            RefundOrderTool.TheName => new RefundOrderTool(),
            CancelOrderTool.TheName => new CancelOrderTool(),
            ResendOrderConfirmationTool.TheName => new ResendOrderConfirmationTool(),
            SaveDraftOrderTool.TheName => new SaveDraftOrderTool(),
            _ => new SendOrderInvoiceTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }
}

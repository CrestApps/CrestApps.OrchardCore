using System.Security.Claims;
using CrestApps.Core.Security;
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
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
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
/// phone, pay link or tender reference), preview a refund without moving money, refund only a stated amount in the
/// order's currency with an idempotency key and the token of a preview of the same refund by the same user, cancel
/// without refunding, and send emails only when the order page would offer it.
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
    };

    private readonly Mock<IOrderStore> _orders = new();
    private readonly Mock<IOrderOperationsService> _operations = new();
    private readonly Mock<IOrderService> _orderService = new();
    private readonly Mock<ITimelineService> _timeline = new();
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

        // The order's notes are internal entries on its timeline.
        _timeline.Setup(timeline => timeline.ListAsync(It.Is<TimelineQuery>(query => query.Subject.Id == "order-1" && query.IncludeInternal), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimelinePage(
                [new TimelineEntry { Kind = TimelineConstants.Kinds.Note, Body = "Gift wrap", AuthorName = "admin", CreatedUtc = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc) }],
                1));
        _orderService.Setup(service => service.AddNoteAsync(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<OrderNoteKind>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    public static TheoryData<string> ToolNames => new()
    {
        ListOrdersTool.TheName,
        GetOrderTool.TheName,
        PreviewRefundTool.TheName,
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
        Assert.False(json.GetProperty("notesTruncated").GetBoolean());
        Assert.Equal(GetOrderTool.UntrustedNote, json.GetProperty("note").GetString());
    }

    [Fact]
    public async Task GetOrder_ReadsOnlyTheLatestNotes_AndPointsToTheTimelineForTheRest()
    {
        // Arrange
        TimelineQuery asked = null;
        _timeline.Setup(timeline => timeline.ListAsync(It.Is<TimelineQuery>(query => query.Subject.Id == "order-1" && query.IncludeInternal), It.IsAny<CancellationToken>()))
            .Callback((TimelineQuery query, CancellationToken _) => asked = query)
            .ReturnsAsync(new TimelinePage(
                [.. Enumerable.Range(1, GetOrderTool.NotesShown).Select(index => new TimelineEntry { Kind = TimelineConstants.Kinds.Note, Body = "Note " + index, AuthorName = "admin", CreatedUtc = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc).AddMinutes(-index) })],
                45));

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(GetOrderTool.TheName, Build(), new() { ["orderId"] = "order-1" }));
        var notes = json.GetProperty("notes");

        // Assert
        Assert.Equal(GetOrderTool.NotesShown, asked.Take);
        Assert.Equal(0, asked.Skip);
        Assert.Equal(GetOrderTool.NotesShown, notes.GetArrayLength());
        Assert.Equal("Note 20", notes[0].GetProperty("text").GetString());
        Assert.True(json.GetProperty("notesTruncated").GetBoolean());
        Assert.Contains("getTimeline", json.GetProperty("notesMore").GetString(), StringComparison.Ordinal);
        Assert.Equal(GetOrderTool.UntrustedNote, json.GetProperty("note").GetString());
    }

    [Fact]
    public async Task GetOrder_SaysWhatDocumentTheInvoiceWasSentAs_AndWhenItWasNotEmailed()
    {
        // Arrange
        _order.Invoice = new OrderInvoice
        {
            SentUtc = new DateTime(2026, 11, 2, 15, 30, 0, DateTimeKind.Utc),
            SentAs = "INV-1001",
            NotEmailed = true,
        };

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(GetOrderTool.TheName, Build(), new() { ["orderNumber"] = "1042" }));

        // Assert
        var invoice = json.GetProperty("invoice");
        Assert.Equal("INV-1001", invoice.GetProperty("sentAs").GetString());
        Assert.True(invoice.GetProperty("notEmailed").GetBoolean());
        Assert.StartsWith("2026-11-02T15:30:00", invoice.GetProperty("sent").GetString(), StringComparison.Ordinal);
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
            ["previewToken"] = await PreviewAsync(20m),
            ["idempotencyKey"] = "refund-1042-damaged",
        };

        // Act
        var first = AgentToolTestContext.ReadJson(await InvokeAsync(RefundOrderTool.TheName, Build(), arguments));
        var again = await InvokeAsync(RefundOrderTool.TheName, Build(), arguments);
        var otherRefund = await InvokeAsync(RefundOrderTool.TheName, Build(), new Dictionary<string, object>(arguments) { ["amount"] = 10m });
        var all = AgentToolTestContext.ReadJson(await InvokeAsync(RefundOrderTool.TheName, Build(), new Dictionary<string, object>(arguments) { ["amount"] = 55m, ["refundAll"] = true, ["previewToken"] = await PreviewAsync(55m, refundAll: true), ["idempotencyKey"] = "refund-1042-all" }));

        // Assert
        Assert.Equal(20m, first.GetProperty("refunded").GetDecimal());
        Assert.StartsWith("Nothing was refunded again", again, StringComparison.Ordinal);
        Assert.Contains("used for a different refund", otherRefund, StringComparison.Ordinal);
        Assert.Equal(55m, all.GetProperty("refunded").GetDecimal());
        _operations.Verify(operations => operations.RefundAsync(_order, 20m, "Box arrived damaged", false, AgentToolTestContext.UserName, false, It.IsAny<CancellationToken>()), Times.Once);
        _operations.Verify(operations => operations.RefundAsync(_order, 55m, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefundOrder_AcceptsTheTokenOfTheSameAmountWrittenWithAnotherScale()
    {
        // Arrange: 25.50 previewed and 25.5 refunded are the same refund.
        var token = await PreviewAsync(25.50m);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(RefundOrderTool.TheName, Build(), new()
        {
            ["orderId"] = "order-1",
            ["amount"] = 25.5m,
            ["currency"] = "USD",
            ["previewToken"] = token,
            ["idempotencyKey"] = "refund-scale",
        }));

        // Assert
        Assert.Equal(25.5m, json.GetProperty("refunded").GetDecimal());
        _operations.Verify(operations => operations.RefundAsync(_order, 25.5m, It.IsAny<string>(), false, It.IsAny<string>(), false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PreviewRefund_ShowsWhereTheMoneyWouldGo_AndRefundsNothing()
    {
        // Arrange
        var planner = new FakeRefundPlanner();

        // Act
        var result = await InvokeAsync(PreviewRefundTool.TheName, Build(planner: planner), new() { ["orderNumber"] = "1042", ["amount"] = 52m, ["currency"] = "USD", ["restock"] = true });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var goesTo = json.GetProperty("goesTo").EnumerateArray().ToList();

        Assert.Equal(55m, json.GetProperty("refundable").GetDecimal());
        Assert.Equal(52m, json.GetProperty("amount").GetDecimal());
        Assert.True(json.GetProperty("restock").GetBoolean());
        Assert.Equal(2, goesTo.Count);
        Assert.Equal("payment", goesTo[0].GetProperty("kind").GetString());
        Assert.Equal("Stripe", goesTo[0].GetProperty("description").GetString());
        Assert.Equal(50m, goesTo[0].GetProperty("amount").GetDecimal());
        Assert.True(goesTo[0].GetProperty("providerCanRefund").GetBoolean());
        Assert.Equal("balance", goesTo[1].GetProperty("kind").GetString());
        Assert.Equal(2m, goesTo[1].GetProperty("amount").GetDecimal());
        Assert.Equal(64, json.GetProperty("previewToken").GetString().Length);
        Assert.False(string.IsNullOrEmpty(json.GetProperty("expiresAt").GetString()));
        Assert.StartsWith("Nothing was refunded.", json.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal((52m, false), Assert.Single(planner.Calls));
        Assert.DoesNotContain("GIFT-SECRET-CODE", result, StringComparison.Ordinal);
        VerifyNoRefund();
    }

    [Theory]
    [InlineData(55.0, "USD", false, "Set refundAll to true")]
    [InlineData(20.0, "EUR", false, "was paid in USD")]
    [InlineData(20.0, "USD", true, "everything still refundable")]
    public async Task PreviewRefund_RefusesWhatRefundOrderWouldRefuse_AndIssuesNoToken(double amount, string currency, bool refundAll, string expected)
    {
        // Act
        var result = await InvokeAsync(PreviewRefundTool.TheName, Build(), new()
        {
            ["orderId"] = "order-1",
            ["amount"] = (decimal)amount,
            ["currency"] = currency,
            ["refundAll"] = refundAll,
        });

        // Assert
        Assert.StartsWith("No refund was previewed", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.DoesNotContain("previewToken\"", result, StringComparison.Ordinal);
        VerifyNoRefund();
    }

    [Fact]
    public async Task RefundOrder_NeedsAPreviewToken()
    {
        // Act
        var result = await InvokeAsync(RefundOrderTool.TheName, Build(), new() { ["orderId"] = "order-1", ["amount"] = 20m, ["currency"] = "USD", ["idempotencyKey"] = "refund-1" });

        // Assert
        Assert.StartsWith("Nothing was refunded: call previewRefund first", result, StringComparison.Ordinal);
        VerifyNoRefund();
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("restock")]
    [InlineData("asStoreCredit")]
    public async Task RefundOrder_RefusesATokenPreviewedForAnotherRefund_AndKeepsTheKeyFree(string changed)
    {
        // Arrange: the order can be refunded as store credit, so only the token refuses the changed refund.
        _operations.Setup(operations => operations.GetMoneyAsync(_order, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderMoney { Currency = "USD", Refundable = 55m, CanRefundAsStoreCredit = true });

        var arguments = new Dictionary<string, object>
        {
            ["orderId"] = "order-1",
            ["amount"] = 20m,
            ["currency"] = "USD",
            ["previewToken"] = await PreviewAsync(20m),
            ["idempotencyKey"] = "refund-1042-key",
        };

        var other = new Dictionary<string, object>(arguments)
        {
            [changed] = changed == "amount" ? (object)10m : true,
        };

        // Act
        var refused = await InvokeAsync(RefundOrderTool.TheName, Build(), other);
        var refunded = AgentToolTestContext.ReadJson(await InvokeAsync(RefundOrderTool.TheName, Build(), arguments));

        // Assert
        Assert.StartsWith("Nothing was refunded: the previewToken was issued for another refund", refused, StringComparison.Ordinal);
        Assert.Equal(20m, refunded.GetProperty("refunded").GetDecimal());
        _operations.Verify(operations => operations.RefundAsync(It.IsAny<Order>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        _operations.Verify(operations => operations.RefundAsync(_order, 20m, It.IsAny<string>(), false, It.IsAny<string>(), false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefundOrder_RefusesATokenThatWasAlreadyUsed()
    {
        // Arrange
        var token = await PreviewAsync(20m);

        // Act
        await InvokeAsync(RefundOrderTool.TheName, Build(), new() { ["orderId"] = "order-1", ["amount"] = 20m, ["currency"] = "USD", ["previewToken"] = token, ["idempotencyKey"] = "refund-a" });
        var second = await InvokeAsync(RefundOrderTool.TheName, Build(), new() { ["orderId"] = "order-1", ["amount"] = 20m, ["currency"] = "USD", ["previewToken"] = token, ["idempotencyKey"] = "refund-b" });

        // Assert
        Assert.StartsWith("Nothing was refunded: the previewToken is not one previewRefund issued to you, or it was already used", second, StringComparison.Ordinal);
        _operations.Verify(operations => operations.RefundAsync(_order, 20m, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefundOrder_RefusesATokenThatExpired()
    {
        // Arrange
        var now = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(() => now);

        var token = await PreviewAsync(20m, clock: clock.Object);

        now = now.AddMinutes(11);

        // Act
        var result = await InvokeAsync(RefundOrderTool.TheName, Build(clock: clock.Object), new() { ["orderId"] = "order-1", ["amount"] = 20m, ["currency"] = "USD", ["previewToken"] = token, ["idempotencyKey"] = "refund-late" });

        // Assert
        Assert.StartsWith("Nothing was refunded: the previewToken expired.", result, StringComparison.Ordinal);
        VerifyNoRefund();
    }

    [Fact]
    public async Task RefundOrder_RefusesATokenPreviewedByAnotherUser()
    {
        // Arrange
        var token = await PreviewAsync(20m);
        var other = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-2"),
            new Claim(ClaimTypes.Name, "clerk"),
        ], "Test"));

        // Act
        var result = await InvokeAsync(RefundOrderTool.TheName, Build(user: other), new() { ["orderId"] = "order-1", ["amount"] = 20m, ["currency"] = "USD", ["previewToken"] = token, ["idempotencyKey"] = "refund-other" });

        // Assert
        Assert.StartsWith("Nothing was refunded: the previewToken is not one previewRefund issued to you", result, StringComparison.Ordinal);
        VerifyNoRefund();
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

    [Fact]
    public async Task RefundOrder_RefusesASecondPreviewOfTheSameRefund_OnceTheFirstWasUsed()
    {
        // Arrange: two previews of the same 20 USD refund, before either is used.
        TrackRefunds();
        var first = await PreviewAsync(20m);
        var second = await PreviewAsync(20m);

        // Act
        var refunded = AgentToolTestContext.ReadJson(await InvokeAsync(RefundOrderTool.TheName, Build(), Refund(20m, first, "refund-first")));
        var again = await InvokeAsync(RefundOrderTool.TheName, Build(), Refund(20m, second, "refund-second"));

        // Assert
        Assert.Equal(20m, refunded.GetProperty("refunded").GetDecimal());
        Assert.StartsWith("Nothing was refunded: the previewToken was issued for another refund, or the order's refunds or payments changed", again, StringComparison.Ordinal);
        _operations.Verify(operations => operations.RefundAsync(It.IsAny<Order>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefundOrder_RefusesATokenWhenTheOrderWasRefundedSinceThePreview()
    {
        // Arrange: 5 USD was refunded on the order's page after the preview.
        TrackRefunds();
        var token = await PreviewAsync(20m);
        _order.Refunds.Add(new OrderRefund { RefundId = "refund-page", Amount = 5m, Currency = "USD" });

        // Act
        var result = await InvokeAsync(RefundOrderTool.TheName, Build(), Refund(20m, token, "refund-after-page"));

        // Assert
        Assert.Contains("the order's refunds or payments changed since the preview", result, StringComparison.Ordinal);
        VerifyNoRefund();
    }

    [Fact]
    public async Task RefundOrder_RefusesATokenWhenTheRefundWouldGoElsewhereThanPreviewed()
    {
        // Arrange: the payment the preview showed is no longer the one the refund would go to.
        var planner = new FakeRefundPlanner();
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(PreviewRefundTool.TheName, Build(planner: planner), new() { ["orderId"] = "order-1", ["amount"] = 20m, ["currency"] = "USD" }));
        planner.Provider = "PayPal";

        // Act
        var result = await InvokeAsync(RefundOrderTool.TheName, Build(planner: planner), Refund(20m, json.GetProperty("previewToken").GetString(), "refund-elsewhere"));

        // Assert
        Assert.Contains("the order's refunds or payments changed since the preview", result, StringComparison.Ordinal);
        VerifyNoRefund();
    }

    [Fact]
    public async Task RefundOrder_ReplaysTheFirstAnswer_WhenARefundThatRanIsRetriedWithItsKey()
    {
        // Arrange: the refund changes the order, so only the key, bound to what was asked, recognizes the retry.
        TrackRefunds();
        var arguments = Refund(20m, await PreviewAsync(20m), "refund-retry");

        // Act
        var first = AgentToolTestContext.ReadJson(await InvokeAsync(RefundOrderTool.TheName, Build(), arguments));
        var retry = await InvokeAsync(RefundOrderTool.TheName, Build(), arguments);

        // Assert
        Assert.Equal(20m, first.GetProperty("refunded").GetDecimal());
        Assert.StartsWith("Nothing was refunded again", retry, StringComparison.Ordinal);
        _operations.Verify(operations => operations.RefundAsync(It.IsAny<Order>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Makes the doubles behave as the order does: a refund is recorded on the order and lowers what is still refundable.
    private void TrackRefunds()
    {
        _operations.Setup(operations => operations.GetMoneyAsync(_order, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new OrderMoney { Currency = "USD", Refundable = 55m - _order.RefundedAmount });
        _operations.Setup(operations => operations.RefundAsync(_order, It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order order, decimal? amount, string _, bool _, string _, bool _, CancellationToken _) =>
            {
                order.Refunds.Add(new OrderRefund { RefundId = "refund-" + order.Refunds.Count, Amount = amount ?? 0m, Currency = "USD" });

                return new OrderOperationResult { Refunded = amount ?? 0m };
            });
    }

    private static Dictionary<string, object> Refund(decimal amount, string token, string key)
        => new()
        {
            ["orderId"] = "order-1",
            ["amount"] = amount,
            ["currency"] = "USD",
            ["previewToken"] = token,
            ["idempotencyKey"] = key,
        };

    private void VerifyNoRefund()
        => _operations.Verify(operations => operations.RefundAsync(It.IsAny<Order>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            ListOrdersTool.TheName => [],
            PreviewRefundTool.TheName => new() { ["orderId"] = "order-1", ["amount"] = 20m, ["currency"] = "USD" },
            RefundOrderTool.TheName => new() { ["orderId"] = "order-1", ["amount"] = 20m, ["currency"] = "USD", ["previewToken"] = "token-1", ["idempotencyKey"] = "refund-1" },
            SaveDraftOrderTool.TheName => new() { ["customerEmail"] = "jane@example.com", ["priceOnly"] = true },
            _ => new() { ["orderId"] = "order-1" },
        };

    // Previews a refund of order 1042 and returns the token refundOrder needs.
    private async Task<string> PreviewAsync(decimal amount, bool refundAll = false, IClock clock = null)
    {
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(PreviewRefundTool.TheName, Build(clock: clock), new()
        {
            ["orderId"] = "order-1",
            ["amount"] = amount,
            ["currency"] = "USD",
            ["refundAll"] = refundAll,
        }));

        return json.GetProperty("previewToken").GetString();
    }

    private ServiceProvider Build(
        bool signedIn = true,
        Func<Permission, object, bool> authorize = null,
        IClock clock = null,
        ClaimsPrincipal user = null,
        IOrderRefundPlanner planner = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        if (clock is null)
        {
            // The site's clock, as the tools read the time from it.
            var siteClock = new Mock<IClock>();
            siteClock.SetupGet(item => item.UtcNow).Returns(() => TimeProvider.System.GetUtcNow().UtcDateTime);
            clock = siteClock.Object;
        }

        services.AddSingleton(clock);

        if (user is not null)
        {
            services.AddSingleton(Mock.Of<IUserAccessor>(accessor => accessor.User == user));
        }

        if (planner is not null)
        {
            services.AddSingleton<IOrderRefundPlanner>(planner);
        }

        services.AddSingleton(_orders.Object);
        services.AddSingleton(_operations.Object);
        services.AddSingleton(_orderService.Object);
        services.AddSingleton(_timeline.Object);
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
            PreviewRefundTool.TheName => new PreviewRefundTool(),
            RefundOrderTool.TheName => new RefundOrderTool(),
            CancelOrderTool.TheName => new CancelOrderTool(),
            ResendOrderConfirmationTool.TheName => new ResendOrderConfirmationTool(),
            SaveDraftOrderTool.TheName => new SaveDraftOrderTool(),
            _ => new SendOrderInvoiceTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }

    // Plans a refund as the order operations service would for an order paid 50 by card and 5 by gift card.
    private sealed class FakeRefundPlanner : IOrderRefundPlanner
    {
        public List<(decimal Amount, bool AsStoreCredit)> Calls { get; } = [];

        public string Provider { get; set; } = "Stripe";

        public Task<OrderRefundPlan> PlanRefundAsync(Order order, decimal amount, bool asStoreCredit, CancellationToken cancellationToken = default)
        {
            Calls.Add((amount, asStoreCredit));

            var plan = new OrderRefundPlan();

            plan.Portions.Add(new OrderRefundPortion(OrderRefundPortionKind.Payment, Provider, Math.Min(amount, 50m), new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc), true));

            if (amount > 50m)
            {
                plan.Portions.Add(new OrderRefundPortion(OrderRefundPortionKind.Balance, "Gift card", amount - 50m, null, null));
            }

            return Task.FromResult(plan);
        }
    }
}

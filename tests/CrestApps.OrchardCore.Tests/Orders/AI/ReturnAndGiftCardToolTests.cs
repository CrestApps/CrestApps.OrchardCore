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
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Orders.AI;

/// <summary>
/// The return, gift card and order diagnosis tools refuse a caller who is not signed in or lacks the page's permission,
/// list returns with the Returns page's filters, never return a gift card's code, guard money with an amount, the card's
/// currency and an idempotency key, and refuse a return's refund through <c>refundOrder</c> when returns are off.
/// </summary>
public sealed class ReturnAndGiftCardToolTests
{
    private readonly GiftCard _card = new()
    {
        ItemId = "card-1",
        Code = "ABCD-EFGH-IJKL-7Q2K",
        InitialValue = 50m,
        Balance = 30m,
        Currency = "USD",
        RecipientEmail = "maria@example.com",
        Activity = [new GiftCardActivity { Amount = 50m, Balance = 50m, Reason = "Issued", CreatedUtc = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc) }],
    };

    private readonly Order _order = new()
    {
        ItemId = "order-1",
        OrderNumber = "1042",
        Status = OrderStatus.Paid,
        Currency = "USD",
        Total = 55m,
    };

    private readonly Mock<IOrderReturnStore> _returns = new();
    private readonly Mock<IGiftCardStore> _cards = new();
    private readonly Mock<IOrderStore> _orders = new();
    private readonly Mock<IOrderOperationsService> _operations = new();
    private OrderReturnQuery _returnQuery;

    public ReturnAndGiftCardToolTests()
    {
        _returns.Setup(store => store.PageAsync(It.IsAny<OrderReturnQuery>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback((OrderReturnQuery query, int _, int _, CancellationToken _) => _returnQuery = query)
            .ReturnsAsync((0, (IReadOnlyList<OrderReturn>)[]));
        _cards.Setup(store => store.FindByIdAsync("card-1", It.IsAny<CancellationToken>())).ReturnsAsync(_card);
        _orders.Setup(store => store.FindByIdAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(_order);
        _operations.Setup(operations => operations.GetMoneyAsync(_order, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderMoney { Currency = "USD", Refundable = 55m });
    }

    public static TheoryData<string, string> Tools => new()
    {
        { ListOrderReturnsTool.TheName, OrderPermissions.ManageOrders.Name },
        { GetOrderReturnTool.TheName, OrderPermissions.ManageOrders.Name },
        { UpdateOrderReturnStatusTool.TheName, OrderPermissions.ManageOrders.Name },
        { ReceiveOrderReturnTool.TheName, OrderPermissions.ManageOrders.Name },
        { DiagnoseOrderTool.TheName, OrderPermissions.ManageOrders.Name },
        { ListGiftCardsTool.TheName, OrderPermissions.ManageGiftCards.Name },
        { GetGiftCardTool.TheName, OrderPermissions.ManageGiftCards.Name },
        { IssueGiftCardTool.TheName, OrderPermissions.ManageGiftCards.Name },
        { UpdateGiftCardTool.TheName, OrderPermissions.ManageGiftCards.Name },
        { AdjustGiftCardBalanceTool.TheName, OrderPermissions.ManageGiftCards.Name },
        { EmailGiftCardTool.TheName, OrderPermissions.ManageGiftCards.Name },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName, string permission)
    {
        // Arrange
        _ = permission;

        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), []);

        // Assert
        Assert.Equal(OrdersAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_returns.Invocations);
        Assert.Empty(_cards.Invocations);
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWithoutThePagesPermission(string toolName, string permission)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != permission);

        // Act
        var result = await InvokeAsync(toolName, services, []);

        // Assert
        Assert.Equal(OrdersAIToolBase.NotAllowedMessage, result);
        Assert.Equal(permission, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_returns.Invocations);
        Assert.Empty(_cards.Invocations);
        Assert.Empty(_orders.Invocations);
    }

    [Fact]
    public async Task ListOrderReturns_UsesTheReturnsPagesFilters()
    {
        // Act
        var declined = AgentToolTestContext.ReadJson(await InvokeAsync(ListOrderReturnsTool.TheName, Build(), new() { ["status"] = "declined", ["search"] = "RMA-26" }));
        var declinedQuery = _returnQuery;
        await InvokeAsync(ListOrderReturnsTool.TheName, Build(), []);
        var openQuery = _returnQuery;
        var wrong = await InvokeAsync(ListOrderReturnsTool.TheName, Build(), new() { ["status"] = "lost" });

        // Assert
        Assert.Equal(OrderReturnStatus.Rejected, declinedQuery.Status);
        Assert.Equal("RMA-26", declinedQuery.Search);
        Assert.Equal(0, declined.GetProperty("total").GetInt32());
        Assert.Null(openQuery.Status);
        Assert.False(openQuery.All);
        Assert.StartsWith("Nothing was listed: status must be", wrong, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOrderReturn_SaysWhenTheReturnIsMissing()
    {
        // Act
        var result = await InvokeAsync(GetOrderReturnTool.TheName, Build(), new() { ["returnId"] = "missing" });

        // Assert
        Assert.Equal("Nothing was done: no return has the id 'missing'. Use listOrderReturns to find it.", result);
    }

    [Fact]
    public async Task GetGiftCard_NeverReturnsTheCode()
    {
        // Act
        var result = await InvokeAsync(GetGiftCardTool.TheName, Build(), new() { ["giftCardId"] = "card-1" });
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        Assert.Equal("7Q2K", json.GetProperty("codeEndsWith").GetString());
        Assert.Equal(30m, json.GetProperty("balance").GetDecimal());
        Assert.DoesNotContain("ABCD", result, StringComparison.Ordinal);
        Assert.DoesNotContain("EFGH", result, StringComparison.Ordinal);
        Assert.Single(json.GetProperty("activity").EnumerateArray());
    }

    [Fact]
    public async Task IssueGiftCard_NeedsAKey_AndAnAmountTheCurrencyAllows()
    {
        // Act
        var noKey = await InvokeAsync(IssueGiftCardTool.TheName, Build(), new() { ["amount"] = 50m, ["currency"] = "USD" });
        var tooPrecise = await InvokeAsync(IssueGiftCardTool.TheName, Build(), new() { ["amount"] = 50.125m, ["currency"] = "USD", ["idempotencyKey"] = "card-maria" });
        var noValue = await InvokeAsync(IssueGiftCardTool.TheName, Build(), new() { ["amount"] = 0m, ["currency"] = "USD", ["idempotencyKey"] = "card-maria" });
        var code = await InvokeAsync(IssueGiftCardTool.TheName, Build(), new() { ["amount"] = 50m, ["currency"] = "USD", ["idempotencyKey"] = "card-maria", ["code"] = "MYOWNCODE1" });

        // Assert
        Assert.StartsWith("Nothing was issued: give a new idempotencyKey", noKey, StringComparison.Ordinal);
        Assert.Contains("more decimal places than USD allows", tooPrecise, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was issued:", noValue, StringComparison.Ordinal);
        Assert.Equal("Nothing was done: 'code' is not an argument of issueGiftCard. Use only the arguments the tool declares.", code);
        _cards.Verify(store => store.CreateAsync(It.IsAny<GiftCard>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdjustGiftCardBalance_RefusesAnotherCurrency_AndMoreThanTheBalance()
    {
        // Act
        var euros = await InvokeAsync(AdjustGiftCardBalanceTool.TheName, Build(), new() { ["giftCardId"] = "card-1", ["amount"] = 10m, ["currency"] = "EUR", ["reason"] = "Apology", ["idempotencyKey"] = "adjust-1" });
        var tooMuch = await InvokeAsync(AdjustGiftCardBalanceTool.TheName, Build(), new() { ["giftCardId"] = "card-1", ["amount"] = -40m, ["currency"] = "USD", ["reason"] = "Correction", ["idempotencyKey"] = "adjust-2" });

        // Assert
        Assert.Equal("Nothing was changed: the card is in USD; give the amount in USD.", euros);
        Assert.Equal("Nothing was changed: the card's balance is only 30.00 USD.", tooMuch);
        Assert.Equal(30m, _card.Balance);
    }

    [Fact]
    public async Task UpdateGiftCard_LeavesTheCardUnchanged_WhenAnArgumentIsWrong()
    {
        // Act
        var result = await InvokeAsync(UpdateGiftCardTool.TheName, Build(), new() { ["giftCardId"] = "card-1", ["isEnabled"] = false, ["expires"] = "not a date" });

        // Assert
        Assert.StartsWith("Nothing was changed: expires must be", result, StringComparison.Ordinal);
        Assert.True(_card.IsEnabled);
        _cards.Verify(store => store.UpdateAsync(It.IsAny<GiftCard>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefundOrder_WithAReturn_IsRefused_WhenReturnsAreOff()
    {
        // Act
        var result = await InvokeAsync(RefundOrderTool.TheName, Build(), new()
        {
            ["orderId"] = "order-1",
            ["amount"] = 20m,
            ["currency"] = "USD",
            ["idempotencyKey"] = "refund-return-1",
            ["returnId"] = "return-1",
        });

        // Assert
        Assert.Equal("Nothing was refunded: returns are not turned on for this site (the Returns feature). Leave out returnId.", result);
        _operations.Verify(operations => operations.RefundAsync(It.IsAny<Order>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DiagnoseOrder_LeavesOutTheLedgers_WithoutManageRefunds()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(DiagnoseOrderTool.TheName, Build(authorize: (asked, _) => asked.Name != "ManageRefunds"), new() { ["orderId"] = "order-1" }));

        // Assert
        Assert.False(json.GetProperty("ledger").GetProperty("available").GetBoolean());
        Assert.Equal("paid", json.GetProperty("status").GetString());
        Assert.Contains(json.GetProperty("findings").EnumerateArray(), finding => finding.GetString().Contains("nothing has shipped", StringComparison.Ordinal));
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(_returns.Object);
        services.AddSingleton(_cards.Object);
        services.AddSingleton(_orders.Object);
        services.AddSingleton(_operations.Object);
        services.AddSingleton(Mock.Of<YesSqlSession>());
        services.AddSingleton<OrdersToolScope>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        services.AddSingleton<IDistributedLock>(new FakeDistributedLock());
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddSingleton(Mock.Of<IClock>(clock => clock.UtcNow == new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc)));

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListOrderReturnsTool.TheName => new ListOrderReturnsTool(),
            GetOrderReturnTool.TheName => new GetOrderReturnTool(),
            UpdateOrderReturnStatusTool.TheName => new UpdateOrderReturnStatusTool(),
            ReceiveOrderReturnTool.TheName => new ReceiveOrderReturnTool(),
            DiagnoseOrderTool.TheName => new DiagnoseOrderTool(),
            ListGiftCardsTool.TheName => new ListGiftCardsTool(),
            GetGiftCardTool.TheName => new GetGiftCardTool(),
            IssueGiftCardTool.TheName => new IssueGiftCardTool(),
            UpdateGiftCardTool.TheName => new UpdateGiftCardTool(),
            AdjustGiftCardBalanceTool.TheName => new AdjustGiftCardBalanceTool(),
            EmailGiftCardTool.TheName => new EmailGiftCardTool(),
            _ => new RefundOrderTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }
}

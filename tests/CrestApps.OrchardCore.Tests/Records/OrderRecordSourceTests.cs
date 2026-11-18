using CrestApps.OrchardCore.Orders;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Records;

/// <summary>
/// Orders are a record kind: a user who manages orders finds them the way the orders list does, and any record that
/// links to an order shows its number and status.
/// </summary>
public sealed class OrderRecordSourceTests
{
    [Fact]
    public async Task ASearch_ReturnsNothing_WhenTheUserMayNotManageOrders()
    {
        // Arrange
        var store = new OrderStoreStub();

        store.Orders.Add(NewOrder("o1", "261013-AAAAAA", OrderStatus.Paid));

        var source = CreateSource(store);
        var customer = PermissionClaimAuthorizationService.User(OrderPermissions.ViewOwnOrders);
        var manager = PermissionClaimAuthorizationService.User(OrderPermissions.ManageOrders);

        // Act
        var found = await source.SearchAsync(new RecordSearch { User = customer }, TestContext.Current.CancellationToken);
        var anonymous = await source.SearchAsync(new RecordSearch(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(found);
        Assert.Empty(anonymous);
        Assert.Equal(0, store.Pages);
        Assert.False(await source.CanSearchAsync(customer, TestContext.Current.CancellationToken));
        Assert.False(await source.CanSearchAsync(null, TestContext.Current.CancellationToken));
        Assert.True(await source.CanSearchAsync(manager, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ASearch_ShowsThePlacedOrdersFirst_AndEveryStatusOnceANumberIsTyped()
    {
        // Arrange
        var store = new OrderStoreStub();

        store.Orders.Add(NewOrder("o1", "261013-AAAAAA", OrderStatus.Paid));
        store.Orders.Add(NewOrder("o2", "261013-BBBBBB", OrderStatus.Canceled));
        store.Orders.Add(NewOrder("o3", "261014-CCCCCC", OrderStatus.Draft));

        var source = CreateSource(store);
        var manager = PermissionClaimAuthorizationService.User(OrderPermissions.ManageOrders);

        // Act
        var first = await source.SearchAsync(new RecordSearch { User = manager, Count = 5 }, TestContext.Current.CancellationToken);
        var typed = await source.SearchAsync(new RecordSearch { Text = " 261013-b ", User = manager, Count = 5 }, TestContext.Current.CancellationToken);
        var none = await source.SearchAsync(new RecordSearch { User = manager, Count = 0 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["261013-AAAAAA"], first.Select(record => record.Title));

        var canceled = Assert.Single(typed);

        Assert.Equal("o2", canceled.Id);
        Assert.Equal("261013-BBBBBB", canceled.Title);
        Assert.Equal("Canceled", canceled.Description);
        Assert.Equal("/Admin/orders/o2", canceled.AdminUrl);
        Assert.Equal("261013-b", store.LastQuery.Search);
        Assert.Equal(5, store.LastTake);
        Assert.Empty(none);
        Assert.Equal(2, store.Pages);
    }

    [Fact]
    public async Task TheSummaries_DescribeTheOrdersFound_ByTheirIds()
    {
        // Arrange
        var store = new OrderStoreStub();

        store.Orders.Add(NewOrder("o1", "261013-AAAAAA", OrderStatus.AwaitingPayment));
        store.Orders.Add(NewOrder("o2", "261013-BBBBBB", OrderStatus.Paid));

        var source = CreateSource(store);

        // Act
        var summaries = await source.GetSummariesAsync(["o2", "missing", "o2", string.Empty], TestContext.Current.CancellationToken);
        var nothing = await source.GetSummariesAsync(null, TestContext.Current.CancellationToken);

        // Assert
        var summary = Assert.Single(summaries);

        Assert.Equal("o2", summary.Key);
        Assert.Equal("261013-BBBBBB", summary.Value.Title);
        Assert.Equal("Paid", summary.Value.Description);
        Assert.Empty(nothing);
        Assert.Equal(OrderConstants.RecordKind, source.Kind);
    }

    private static OrderRecordSource CreateSource(IOrderStore store)
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>();

        httpContextAccessor.SetupGet(accessor => accessor.HttpContext).Returns(new DefaultHttpContext());

        return new OrderRecordSource(
            store,
            new PermissionClaimAuthorizationService(),
            new TemplateLinkGenerator((_, values) => $"/Admin/orders/{values["orderId"]}"),
            httpContextAccessor.Object,
            new StringLocalizer<OrderRecordSource>(new SourceTextLocalizerFactory()));
    }

    private static Order NewOrder(string id, string number, OrderStatus status)
        => new()
        {
            ItemId = id,
            OrderNumber = number,
            Status = status,
        };

    // Finds orders the way the YesSql store does: by status and by the start of the number. It doesn't override
    // GetAsync, so the summaries read through the order store's default.
    private sealed class OrderStoreStub : IOrderStore
    {
        public List<Order> Orders { get; } = [];

        public OrderQuery LastQuery { get; private set; }

        public int LastTake { get; private set; }

        public int Pages { get; private set; }

        public ValueTask<Order> FindByIdAsync(string orderId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Orders.FirstOrDefault(order => order.ItemId == orderId));

        public Task<Order> FindByNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(Orders.FirstOrDefault(order => order.OrderNumber == orderNumber));

        public Task<IReadOnlyList<Order>> ListUnpaidByCartAsync(string cartId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Order>>([]);

        public Task<(int Count, IReadOnlyList<Order> Orders)> PageAsync(OrderQuery query, int skip, int take, CancellationToken cancellationToken = default)
        {
            Pages++;
            LastQuery = query;
            LastTake = take;

            var matches = Orders
                .Where(order => query.Statuses is not { Count: > 0 } || query.Statuses.Contains(order.Status))
                .Where(order => string.IsNullOrEmpty(query.Search) || order.OrderNumber.StartsWith(query.Search, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return Task.FromResult<(int, IReadOnlyList<Order>)>((matches.Length, matches.Skip(skip).Take(take).ToArray()));
        }

        public ValueTask CreateAsync(Order order, CancellationToken cancellationToken = default)
        {
            Orders.Add(order);

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(Order order, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}

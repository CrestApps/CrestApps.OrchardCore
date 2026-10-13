using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Orders.ViewModels;

public class OrdersIndexViewModel
{
    public OrdersIndexOptions Options { get; set; } = new();

    public IReadOnlyList<Order> Orders { get; set; } = [];

    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets whether the list is the signed-in customer's own orders.
    /// </summary>
    public bool IsOwnOrders { get; set; }

    [BindNever]
    public IShape Pager { get; set; }
}

public class OrdersIndexOptions
{
    public string Search { get; set; }

    public OrderStatusFilter Status { get; set; }

    [BindNever]
    public List<SelectListItem> Statuses { get; set; } = [];
}

public enum OrderStatusFilter
{
    /// <summary>
    /// Paid orders and orders whose checkout started.
    /// </summary>
    Placed,

    Paid,

    AwaitingPayment,

    Canceled,

    All,
}

public class OrderDetailViewModel
{
    public Order Order { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer manages orders, rather than looking at one of their own.
    /// </summary>
    public bool CanManage { get; set; }
}

public class OrderConfirmationViewModel
{
    public string OrderId { get; set; }

    public string OrderNumber { get; set; }

    /// <summary>
    /// Gets or sets whether the order belongs to a signed-in customer, who can find it again under My Orders.
    /// </summary>
    public bool IsSignedIn { get; set; }
}

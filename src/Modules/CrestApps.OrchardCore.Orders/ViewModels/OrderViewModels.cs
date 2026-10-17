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

    /// <summary>
    /// Gets or sets the signed link a guest opens their order with.
    /// </summary>
    public string GuestOrderUrl { get; set; }

    /// <summary>
    /// Gets or sets the link to create an account, for a guest on a site that lets visitors register.
    /// </summary>
    public string RegisterUrl { get; set; }
}

public class OrderSettingsViewModel
{
    public bool SendConfirmations { get; set; }
}

public class OrderViewViewModel
{
    public Order Order { get; set; }

    /// <summary>
    /// Gets or sets the link to pay what is still owed on the order, or <see langword="null"/>.
    /// </summary>
    public string PayUrl { get; set; }

    /// <summary>
    /// Gets or sets the link to create an account, for a guest on a site that lets visitors register.
    /// </summary>
    public string RegisterUrl { get; set; }
}

public class FindOrderViewModel
{
    public string Email { get; set; }
}

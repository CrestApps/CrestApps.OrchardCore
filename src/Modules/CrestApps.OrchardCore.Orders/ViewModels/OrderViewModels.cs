using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
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

    /// <summary>
    /// Gets or sets the orders, pager and region shapes the <c>admin-list</c> tag renders, in the
    /// layout (List or Grid) the site or the user picked.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
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

    /// <summary>
    /// Gets or sets what other modules add to the order's page, such as its shipments.
    /// </summary>
    public IShape Sections { get; set; }

    /// <summary>
    /// Gets or sets what can still be refunded and what is still owed, for a viewer who manages orders.
    /// </summary>
    public CrestApps.OrchardCore.Orders.Services.OrderMoney Money { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the site can email the confirmation again.
    /// </summary>
    public bool CanResendConfirmation { get; set; }
}

public class OrderRefundInputModel
{
    public decimal? Amount { get; set; }

    public string Reason { get; set; }

    public bool Restock { get; set; }
}

public class OrderCancelInputModel
{
    public string Note { get; set; }

    public bool Refund { get; set; }

    public bool Restock { get; set; }
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

    /// <summary>
    /// Gets or sets the token that lets a guest make an account from this page and take the order into it, or
    /// <see langword="null"/> when they can't (not a guest, no email, or the site doesn't let visitors register).
    /// </summary>
    public string AccountToken { get; set; }

    /// <summary>
    /// Gets or sets the email the account would get: the order's.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the page to come back to after making the account fails.
    /// </summary>
    public string ReturnUrl { get; set; }
}

public class OrderCreateAccountViewModel
{
    public string Token { get; set; }

    public string Password { get; set; }

    public string ConfirmPassword { get; set; }
}

public class OrderSettingsViewModel
{
    public bool SendConfirmations { get; set; }

    public bool SendRecoveryEmails { get; set; }

    public int RecoveryDelayHours { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether the site has the base URL recovery emails link back through.
    /// </summary>
    [BindNever]
    public bool HasBaseUrl { get; set; }
}

public class OrderViewViewModel
{
    public Order Order { get; set; }

    /// <summary>
    /// Gets or sets what other modules add to the order's page, such as its shipments and their tracking links.
    /// </summary>
    public IShape Sections { get; set; }

    /// <summary>
    /// Gets or sets the link to pay what is still owed on the order, or <see langword="null"/>.
    /// </summary>
    public string PayUrl { get; set; }

    public string RegisterUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the order is shown in the signed-in customer's account, beside its menu.
    /// </summary>
    public bool InAccount { get; set; }
}

public class AccountOrdersViewModel
{
    public IReadOnlyList<Order> Orders { get; set; } = [];

    public IShape Pager { get; set; }
}

public class FindOrderViewModel
{
    public string Email { get; set; }
}

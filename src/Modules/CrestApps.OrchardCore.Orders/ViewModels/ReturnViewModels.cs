using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Orders.ViewModels;

/// <summary>
/// The buyer's return form, the model of the <c>OrderReturnRequest</c> shape a theme can override.
/// </summary>
public class OrderReturnRequestViewModel
{
    public List<OrderReturnRequestLineViewModel> Lines { get; set; } = [];

    public OrderReturnResolution Resolution { get; set; }

    public string ExchangeFor { get; set; }

    public string Comment { get; set; }

    [BindNever]
    public Order Order { get; set; }

    [BindNever]
    public IReadOnlyList<string> Reasons { get; set; } = [];

    [BindNever]
    public bool AllowExchanges { get; set; }

    [BindNever]
    public int WindowDays { get; set; }

    [BindNever]
    public IReadOnlyList<string> Problems { get; set; } = [];

    [BindNever]
    public string BackUrl { get; set; }
}

public class OrderReturnRequestLineViewModel
{
    public string LineId { get; set; }

    public int Quantity { get; set; }

    public string Reason { get; set; }

    [BindNever]
    public string Title { get; set; }

    [BindNever]
    public string Sku { get; set; }

    [BindNever]
    public int Returnable { get; set; }
}

/// <summary>
/// The returns of an order on its pages: the buyer's, with a way to start one, and the store's.
/// </summary>
public class OrderReturnsViewModel
{
    public Order Order { get; set; }

    public IReadOnlyList<OrderReturn> Returns { get; set; } = [];

    public string RequestUrl { get; set; }

    public string Instructions { get; set; }

    public string WithdrawUrlFormat { get; set; }
}

public class OrderReturnIndexOptions
{
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the returns shown: a status, <c>all</c>, or empty for the open ones.
    /// </summary>
    public string Show { get; set; }

    [BindNever]
    public List<SelectListItem> Filters { get; set; } = [];
}

public class OrderReturnIndexViewModel
{
    public OrderReturnIndexOptions Options { get; set; }

    public AdminListContext List { get; set; }
}

public class OrderReturnDetailViewModel
{
    public OrderReturn Return { get; set; }

    public Order Order { get; set; }

    public IReadOnlyList<SelectListItem> Locations { get; set; } = [];

    public decimal SuggestedRefund { get; set; }

    public decimal Refundable { get; set; }

    public string Instructions { get; set; }
}

public class OrderReturnRowViewModel
{
    public OrderReturn Return { get; set; }
}

public class ReturnSettingsViewModel
{
    public int WindowDays { get; set; }

    public string Reasons { get; set; }

    public bool AllowExchanges { get; set; }

    public string Instructions { get; set; }
}

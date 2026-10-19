using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Orders.ViewModels;

public class GiftCardsIndexOptions
{
    public string Search { get; set; }

    public GiftCardStatusFilter Status { get; set; }

    [BindNever]
    public List<SelectListItem> Statuses { get; set; } = [];
}

public class GiftCardsIndexViewModel
{
    public GiftCardsIndexOptions Options { get; set; }

    [BindNever]
    public dynamic Pager { get; set; }

    [BindNever]
    public AdminListContext<GiftCard> List { get; set; }
}

public class GiftCardCreateViewModel
{
    public decimal? Value { get; set; }

    public string Currency { get; set; }

    public string Code { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    public string RecipientName { get; set; }

    [EmailAddress]
    public string RecipientEmail { get; set; }

    public string Message { get; set; }

    public string Note { get; set; }

    public bool SendEmail { get; set; } = true;
}

public class GiftCardEditViewModel
{
    [BindNever]
    public string ItemId { get; set; }

    [BindNever]
    public string Code { get; set; }

    [BindNever]
    public decimal InitialValue { get; set; }

    [BindNever]
    public decimal Balance { get; set; }

    [BindNever]
    public string Currency { get; set; }

    [BindNever]
    public string OrderId { get; set; }

    [BindNever]
    public string OrderNumber { get; set; }

    [BindNever]
    public DateTime CreatedUtc { get; set; }

    [BindNever]
    public IReadOnlyList<GiftCardActivity> Activity { get; set; } = [];

    public bool IsEnabled { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    public string RecipientName { get; set; }

    [EmailAddress]
    public string RecipientEmail { get; set; }

    public string Note { get; set; }
}

public class CheckoutGiftCardViewModel
{
    // Not "Code": the promotion code box on the same step binds that name.
    public string GiftCardCode { get; set; }

    [BindNever]
    public string Currency { get; set; }

    [BindNever]
    public IReadOnlyList<AppliedGiftCardViewModel> Applied { get; set; } = [];
}

public class AppliedGiftCardViewModel
{
    public string ItemId { get; set; }

    public string LastCharacters { get; set; }

    /// <summary>
    /// Gets or sets what the card pays toward this order.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets what's left on it after this order.
    /// </summary>
    public decimal Remaining { get; set; }
}

using CrestApps.OrchardCore.Markets.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Markets.ViewModels;

public class MarketsIndexViewModel
{
    public AdminListContext<Market> List { get; set; }

    public string StoreCurrency { get; set; }
}

public class MarketEditViewModel
{
    [BindNever]
    public string ItemId { get; set; }

    public bool IsNew
        => string.IsNullOrEmpty(ItemId);

    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the market's countries, as two-letter codes separated by commas or spaces.
    /// </summary>
    public string Countries { get; set; }

    public string Currency { get; set; }

    public decimal ExchangeRate { get; set; } = 1m;

    public MarketPriceRounding Rounding { get; set; }

    public string Culture { get; set; }

    public bool PricesIncludeTax { get; set; }

    public bool IsEnabled { get; set; } = true;

    [BindNever]
    public string StoreCurrency { get; set; }

    [BindNever]
    public IList<SelectListItem> Roundings { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Cultures { get; set; } = [];
}

/// <summary>
/// "Not sold in" on a product's editor.
/// </summary>
public class ProductMarketsEditViewModel
{
    public List<string> NotSoldIn { get; set; } = [];

    [BindNever]
    public IReadOnlyList<Market> Markets { get; set; } = [];
}

/// <summary>
/// The market picker on the storefront's navigation bar.
/// </summary>
public class MarketPickerViewModel
{
    public IReadOnlyList<Market> Markets { get; set; } = [];

    public Market Current { get; set; }

    public string StoreCurrency { get; set; }

    public string ReturnUrl { get; set; }
}

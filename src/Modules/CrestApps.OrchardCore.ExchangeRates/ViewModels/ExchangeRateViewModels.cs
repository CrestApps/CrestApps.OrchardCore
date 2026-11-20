using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ExchangeRates.ViewModels;

/// <summary>
/// The editor of an exchange rate.
/// </summary>
public class ExchangeRateViewModel
{
    public string Currency { get; set; }

    public string BaseCurrency { get; set; }

    public decimal Rate { get; set; }

    public DateOnly Date { get; set; }

    public string Source { get; set; }
}

/// <summary>
/// The exchange rate settings editor.
/// </summary>
public class ExchangeRateSettingsViewModel
{
    public string BaseCurrency { get; set; }

    public string Currencies { get; set; }

    public string ProviderName { get; set; }

    public List<SelectListItem> Providers { get; set; } = [];
}

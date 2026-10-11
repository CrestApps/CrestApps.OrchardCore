namespace CrestApps.OrchardCore.Products.ViewModels;

/// <summary>
/// Represents the editor view model for a managed currency.
/// </summary>
public class CurrencyEntryViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the currency is new.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency code.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the friendly display name.
    /// </summary>
    public string DisplayName { get; set; }
}

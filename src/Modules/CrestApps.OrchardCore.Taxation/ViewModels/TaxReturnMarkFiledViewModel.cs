namespace CrestApps.OrchardCore.Taxation.ViewModels;

/// <summary>
/// The form that marks a tax return as filed, built by display drivers so its fields are grouped in a card by placement.
/// </summary>
public class TaxReturnMarkFiledViewModel
{
    /// <summary>
    /// Gets or sets today, the day the return is marked as filed on by default.
    /// </summary>
    public DateOnly Today { get; set; }
}

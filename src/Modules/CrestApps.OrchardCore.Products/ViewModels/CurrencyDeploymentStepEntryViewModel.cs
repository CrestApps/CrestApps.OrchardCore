namespace CrestApps.OrchardCore.Products.ViewModels;

/// <summary>
/// Represents a selectable currency entry in the deployment-step editor.
/// </summary>
public class CurrencyDeploymentStepEntryViewModel
{
    /// <summary>
    /// Gets or sets the currency identifier.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency code.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the friendly display name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the currency is selected.
    /// </summary>
    public bool IsSelected { get; set; }
}

namespace CrestApps.OrchardCore.Products.ViewModels;

/// <summary>
/// Represents the editor view model for the currencies deployment step.
/// </summary>
public class CurrencyDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether every currency should be included.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the available currencies.
    /// </summary>
    public CurrencyDeploymentStepEntryViewModel[] Currencies { get; set; }
}

using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// The installment plan settings editor.
/// </summary>
public class InstallmentPlanSettingsViewModel
{
    /// <summary>
    /// Gets or sets the retry delays in days, comma separated.
    /// </summary>
    public string RetryDays { get; set; }

    /// <summary>
    /// Gets or sets the collection method a new plan starts with.
    /// </summary>
    public InstallmentCollectionMethod DefaultCollectionMethod { get; set; }
}

using CrestApps.OrchardCore.Subscriptions.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

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

    /// <summary>
    /// Gets or sets the transaction sources whose balances a customer may pay in installments at checkout.
    /// </summary>
    public string[] CheckoutSources { get; set; } = [];

    /// <summary>
    /// Gets or sets the numbers of payments a customer may choose from at checkout, comma separated.
    /// </summary>
    public string CheckoutInstallmentCounts { get; set; }

    /// <summary>
    /// Gets or sets how often the payments chosen at checkout fall due.
    /// </summary>
    public InstallmentFrequency CheckoutFrequency { get; set; }

    /// <summary>
    /// Gets or sets the smallest balance that may be paid in installments at checkout.
    /// </summary>
    public decimal CheckoutMinimumAmount { get; set; }

    /// <summary>
    /// Gets or sets the registered transaction sources to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AvailableSources { get; set; } = [];
}

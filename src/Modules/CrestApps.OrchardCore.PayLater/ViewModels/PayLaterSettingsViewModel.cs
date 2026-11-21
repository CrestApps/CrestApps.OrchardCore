using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.PayLater.ViewModels;

/// <summary>
/// The editor view model for the Pay Later settings.
/// </summary>
public class PayLaterSettingsViewModel
{
    /// <summary>
    /// Gets or sets whether Pay Later is offered at checkout.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the number of days a Pay Later balance is allowed before it is due.
    /// </summary>
    public int NetTermDays { get; set; }

    public bool RequireSignedIn { get; set; }

    public decimal? MaxOrderAmount { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the payment term a Pay Later balance follows when the payer has none.
    /// </summary>
    public string PaymentTermId { get; set; }

    /// <summary>
    /// Gets or sets the enabled payment terms to choose from, empty when the Payment Terms feature is off.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> PaymentTerms { get; set; } = [];
}

using CrestApps.OrchardCore.Fulfillment.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Fulfillment.ViewModels;

/// <summary>
/// The search and the status filter of the Fulfillment requests page.
/// </summary>
public class FulfillmentRequestsOptions
{
    /// <summary>
    /// Gets or sets the text an order number starts with.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the status to show, or <see langword="null"/> for all.
    /// </summary>
    public FulfillmentRequestStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the statuses to pick from.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Statuses { get; set; } = [];
}

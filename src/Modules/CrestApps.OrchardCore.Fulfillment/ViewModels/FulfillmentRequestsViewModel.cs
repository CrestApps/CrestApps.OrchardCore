using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Fulfillment.ViewModels;

/// <summary>
/// The Commerce › Fulfillment Requests page.
/// </summary>
public class FulfillmentRequestsViewModel
{
    /// <summary>
    /// Gets or sets the search and the status filter.
    /// </summary>
    public FulfillmentRequestsOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the list.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}

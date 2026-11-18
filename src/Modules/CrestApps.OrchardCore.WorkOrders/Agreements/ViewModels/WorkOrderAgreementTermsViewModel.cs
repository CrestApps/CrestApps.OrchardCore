using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.WorkOrders.Agreements.ViewModels;

/// <summary>
/// The Work Orders terms of an agreement on its editor.
/// </summary>
public class WorkOrderAgreementTermsViewModel
{
    /// <summary>
    /// Gets or sets the agreement, once saved.
    /// </summary>
    [BindNever]
    public string AgreementId { get; set; }

    /// <summary>
    /// Gets or sets the customer.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the customer's name.
    /// </summary>
    [BindNever]
    public string CustomerName { get; set; }

    /// <summary>
    /// Gets or sets whether customers can be chosen.
    /// </summary>
    [BindNever]
    public bool CanChooseCustomer { get; set; }

    public string ContactName { get; set; }

    public string ContactPhone { get; set; }

    public string ContactEmail { get; set; }

    public string AddressLine1 { get; set; }

    public string City { get; set; }

    public string Region { get; set; }

    public string PostalCode { get; set; }

    public string Country { get; set; }

    public string AccessNotes { get; set; }

    public List<string> AssetIds { get; set; } = [];

    public AgreementPricing Pricing { get; set; }

    public decimal PricePerVisit { get; set; }

    public List<ScheduleEditModel> Schedules { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> AssetOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> RuleOptions { get; set; } = [];
}

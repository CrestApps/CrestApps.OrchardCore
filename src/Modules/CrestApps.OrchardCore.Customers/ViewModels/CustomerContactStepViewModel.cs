using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Customers.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Customers.ViewModels;

public class CustomerContactStepViewModel
{
    public string Name { get; set; }

    public string Email { get; set; }

    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the address-book entry to bill, or empty for the address entered below.
    /// </summary>
    public string BillingAddressId { get; set; }

    public AddressEditorViewModel BillingAddress { get; set; } = new();

    public bool SaveBillingAddress { get; set; }

    [BindNever]
    public bool IsSignedIn { get; set; }

    [BindNever]
    public BillingAddressRequirement Requirement { get; set; }

    [BindNever]
    public IReadOnlyList<CustomerAddressSummary> SavedAddresses { get; set; } = [];

    [BindNever]
    public bool CanSaveAddress { get; set; }

    [BindNever]
    public IShape BillingAddressEditor { get; set; }
}

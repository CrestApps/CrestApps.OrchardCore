using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Customers.Drivers;

/// <summary>
/// Builds the editor of a customer's address on the administrator's page, grouped in cards by their placement. The
/// address itself is laid out for its country by the address editor the card holds.
/// </summary>
internal sealed class CustomerAddressEditDisplayDriver : DisplayDriver<CustomerAddressViewModel>
{
    internal readonly IStringLocalizer S;

    public CustomerAddressEditDisplayDriver(IStringLocalizer<CustomerAddressEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CustomerAddressViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<CustomerAddressViewModel>("CustomerAddress_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Address"], "10")),
            Initialize<CustomerAddressViewModel>("CustomerAddressDefaults_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Defaults"], "20")));
    }

    public override async Task<IDisplayResult> UpdateAsync(CustomerAddressViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Label,
            m => m.IsDefaultBilling,
            m => m.IsDefaultShipping);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the address book validates.
    protected override void BuildPrefix(CustomerAddressViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(CustomerAddressViewModel source, CustomerAddressViewModel model)
    {
        model.CustomerId = source.CustomerId;
        model.CustomerName = source.CustomerName;
        model.AddressId = source.AddressId;
        model.IsNew = source.IsNew;
        model.IsOwn = source.IsOwn;
        model.AddressEditor = source.AddressEditor;
        model.Label = source.Label;
        model.Address = source.Address;
        model.IsDefaultBilling = source.IsDefaultBilling;
        model.IsDefaultShipping = source.IsDefaultShipping;
    }
}

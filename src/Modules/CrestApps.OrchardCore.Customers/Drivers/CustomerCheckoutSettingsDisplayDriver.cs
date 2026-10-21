using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Customers.Drivers;

/// <summary>
/// Settings › Commerce › Customers: what an order's checkout asks the buyer.
/// </summary>
public sealed class CustomerCheckoutSettingsDisplayDriver : CustomerSettingsDisplayDriverBase<CustomerCheckoutSettings>
{
    public CustomerCheckoutSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
        : base(httpContextAccessor, authorizationService)
    {
    }

    protected override string SettingsGroupId
        => CustomersConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, CustomerCheckoutSettings settings, BuildEditorContext context)
        => Initialize<CustomerCheckoutSettingsViewModel>("CustomerCheckoutSettings_Edit", model =>
        {
            model.BillingAddress = settings.BillingAddress;
        }).Location("Content:1")
        .RenderWhen(CanManageCustomersAsync)
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, CustomerCheckoutSettings settings, UpdateEditorContext context)
    {
        if (!await CanManageCustomersAsync())
        {
            return null;
        }

        var model = new CustomerCheckoutSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.BillingAddress = Enum.IsDefined(model.BillingAddress) ? model.BillingAddress : BillingAddressRequirement.Optional;

        return Edit(site, settings, context);
    }
}

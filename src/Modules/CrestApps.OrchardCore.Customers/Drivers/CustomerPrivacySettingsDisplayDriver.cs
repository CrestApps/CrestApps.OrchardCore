using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Customers.Drivers;

/// <summary>
/// Settings › Commerce › Customers: how long guests' personal data is kept.
/// </summary>
public sealed class CustomerPrivacySettingsDisplayDriver : CustomerSettingsDisplayDriverBase<CustomerPrivacySettings>
{
    private const int MaxMonths = 1200;

    internal readonly IStringLocalizer S;

    public CustomerPrivacySettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<CustomerPrivacySettingsDisplayDriver> stringLocalizer)
        : base(httpContextAccessor, authorizationService)
    {
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => CustomersConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, CustomerPrivacySettings settings, BuildEditorContext context)
        => Initialize<CustomerPrivacySettingsViewModel>("CustomerPrivacySettings_Edit", model =>
        {
            model.InactiveGuestMonths = settings.InactiveGuestMonths;
            model.GuestOrderMonths = settings.GuestOrderMonths;
        }).Location("Content:5")
        .RenderWhen(CanManageCustomersAsync)
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, CustomerPrivacySettings settings, UpdateEditorContext context)
    {
        if (!await CanManageCustomersAsync())
        {
            return null;
        }

        var model = new CustomerPrivacySettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.InactiveGuestMonths is < 0 or > MaxMonths)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.InactiveGuestMonths), S["Enter a number of months from 0 to {0}.", MaxMonths]);
        }

        if (model.GuestOrderMonths is < 0 or > MaxMonths)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.GuestOrderMonths), S["Enter a number of months from 0 to {0}.", MaxMonths]);
        }

        if (context.Updater.ModelState.IsValid)
        {
            settings.InactiveGuestMonths = model.InactiveGuestMonths;
            settings.GuestOrderMonths = model.GuestOrderMonths;
        }

        return Edit(site, settings, context);
    }
}

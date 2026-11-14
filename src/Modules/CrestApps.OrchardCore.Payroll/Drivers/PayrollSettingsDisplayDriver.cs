using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// Settings &gt; Payroll: separation of duties and the defaults of a new employer.
/// </summary>
internal sealed class PayrollSettingsDisplayDriver : SiteDisplayDriver<PayrollSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public PayrollSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => PayrollConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, PayrollSettings settings, BuildEditorContext context)
        => Initialize<PayrollSettingsViewModel>("PayrollSettings_Edit", model =>
        {
            model.AllowPreparerToApprove = settings.AllowPreparerToApprove;
            model.AllowChangingOwnPay = settings.AllowChangingOwnPay;
            model.DefaultCountryPack = settings.DefaultCountryPack;
            model.DefaultCurrency = settings.DefaultCurrency;
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, PayrollPermissions.ManagePayrollSettings))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, PayrollSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, PayrollPermissions.ManagePayrollSettings))
        {
            return null;
        }

        var model = new PayrollSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.AllowPreparerToApprove = model.AllowPreparerToApprove;
        settings.AllowChangingOwnPay = model.AllowChangingOwnPay;
        settings.DefaultCountryPack = string.IsNullOrWhiteSpace(model.DefaultCountryPack) ? null : model.DefaultCountryPack.Trim().ToUpperInvariant();
        settings.DefaultCurrency = string.IsNullOrWhiteSpace(model.DefaultCurrency) ? null : model.DefaultCurrency.Trim().ToUpperInvariant();

        return Edit(site, settings, context);
    }
}

using System.Globalization;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// Settings &gt; Payroll: separation of duties and the defaults of a new employer.
/// </summary>
internal sealed class PayrollSettingsDisplayDriver : SiteDisplayDriver<PayrollSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<IPayrollTaxProvider> _taxProviders;
    private readonly PayRunApprovalPolicySeeder _policySeeder;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public PayrollSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IEnumerable<IPayrollTaxProvider> taxProviders,
        PayRunApprovalPolicySeeder policySeeder,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<PayrollSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _taxProviders = taxProviders;
        _policySeeder = policySeeder;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => PayrollConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, PayrollSettings settings, BuildEditorContext context)
        => Initialize<PayrollSettingsViewModel>("PayrollSettings_Edit", model =>
        {
            model.AllowPreparerToApprove = settings.AllowPreparerToApprove;
            model.AllowChangingOwnPay = settings.AllowChangingOwnPay;
            model.EmployeeReview = settings.EmployeeReview;
            model.DefaultCountryPack = settings.DefaultCountryPack;
            model.DefaultCurrency = settings.DefaultCurrency;
            model.TaxProvider = settings.TaxProvider;
            model.MinimumNetPay = settings.MinimumNetPay == 0 ? null : settings.MinimumNetPay.ToString("0.##", CultureInfo.InvariantCulture);
            model.MinimumHourlyWage = settings.MinimumHourlyWage?.ToString("0.####", CultureInfo.InvariantCulture);
            model.MaximumTipCredit = settings.MaximumTipCredit?.ToString("0.####", CultureInfo.InvariantCulture);
            model.TaxProviders = [.. _taxProviders.Select(provider => new SelectListItem(_localizerFactory.Localize(provider.DisplayName).Value, provider.Name, provider.Name == settings.TaxProvider))];
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

        if (settings.AllowPreparerToApprove != model.AllowPreparerToApprove)
        {
            await _policySeeder.SyncAsync(model.AllowPreparerToApprove);
        }

        settings.AllowPreparerToApprove = model.AllowPreparerToApprove;
        settings.AllowChangingOwnPay = model.AllowChangingOwnPay;
        settings.EmployeeReview = model.EmployeeReview;
        settings.DefaultCountryPack = string.IsNullOrWhiteSpace(model.DefaultCountryPack) ? null : model.DefaultCountryPack.Trim().ToUpperInvariant();
        settings.DefaultCurrency = string.IsNullOrWhiteSpace(model.DefaultCurrency) ? null : model.DefaultCurrency.Trim().ToUpperInvariant();
        settings.TaxProvider = string.IsNullOrEmpty(model.TaxProvider) ? null : model.TaxProvider;

        if (string.IsNullOrWhiteSpace(model.MinimumNetPay))
        {
            settings.MinimumNetPay = 0;
        }
        else if (decimal.TryParse(model.MinimumNetPay.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var minimum))
        {
            settings.MinimumNetPay = minimum;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MinimumNetPay), S["The least net pay is an amount of zero or more, with a dot for decimals."]);
        }

        settings.MinimumHourlyWage = Amount(model.MinimumHourlyWage, nameof(model.MinimumHourlyWage), context);
        settings.MaximumTipCredit = Amount(model.MaximumTipCredit, nameof(model.MaximumTipCredit), context);

        return Edit(site, settings, context);
    }

    private decimal? Amount(string text, string field, UpdateEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        context.Updater.ModelState.AddModelError(Prefix, field, S["Type an amount of zero or more, with a dot for decimals."]);

        return null;
    }
}

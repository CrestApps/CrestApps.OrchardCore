using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The Payroll card of the tax form editor, for a form whose source is Payroll: whose pay it reports, only people taxed
/// in a jurisdiction, the state and local rows of a W-2, and the prefix pay codes report to its fields by.
/// </summary>
internal sealed class PayrollTaxFormSettingsDisplayDriver : DisplayDriver<TaxForm>
{
    internal readonly IStringLocalizer S;

    public PayrollTaxFormSettingsDisplayDriver(IStringLocalizer<PayrollTaxFormSettingsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TaxForm form, BuildEditorContext context)
        => Initialize<PayrollTaxFormSettingsViewModel>("PayrollTaxFormSettings_Edit", model =>
        {
            var settings = form.GetPayrollSettings();

            model.Workers = settings.Workers.ToString();
            model.PeopleJurisdiction = settings.PeopleJurisdiction;
            model.ListJurisdictionAmounts = settings.ListJurisdictionAmounts;
            model.PayCodeFieldPrefix = settings.PayCodeFieldPrefix;
        }).Location(l => l.Zone("Content", "2").Card(S["Payroll"], "20"))
        .RenderWhen(() => Task.FromResult(IsPayroll(form)));

    public override async Task<IDisplayResult> UpdateAsync(TaxForm form, UpdateEditorContext context)
    {
        if (!IsPayroll(form))
        {
            return Edit(form, context);
        }

        var model = new PayrollTaxFormSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        form.Put(new PayrollTaxFormSettings
        {
            Workers = Enum.TryParse<PayrollTaxFormWorkers>(model.Workers, out var workers) ? workers : PayrollTaxFormWorkers.Employees,
            PeopleJurisdiction = string.IsNullOrWhiteSpace(model.PeopleJurisdiction) ? null : model.PeopleJurisdiction.Trim().ToUpperInvariant(),
            ListJurisdictionAmounts = model.ListJurisdictionAmounts,
            PayCodeFieldPrefix = string.IsNullOrWhiteSpace(model.PayCodeFieldPrefix) ? null : model.PayCodeFieldPrefix.Trim(),
        });

        return Edit(form, context);
    }

    // A new form gets Payroll's card when Payroll is its source, the first one listed.
    private static bool IsPayroll(TaxForm form)
        => string.IsNullOrEmpty(form.Source) || string.Equals(form.Source, PayrollConstants.TaxFormSource, StringComparison.OrdinalIgnoreCase);
}

using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Taxation.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Drivers;

/// <summary>
/// With Tax Returns: the filing schedule of a tax registration (how often it files, when its returns are due and on
/// which forms) in its editor, and its frequency on its row.
/// </summary>
internal sealed class MerchantTaxRegistrationFilingDisplayDriver : DisplayDriver<MerchantTaxRegistration>
{
    private readonly ITaxReturnService _returns;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public MerchantTaxRegistrationFilingDisplayDriver(
        ITaxReturnService returns,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<MerchantTaxRegistrationFilingDisplayDriver> stringLocalizer)
    {
        _returns = returns;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(MerchantTaxRegistration registration, BuildDisplayContext context)
    {
        if (registration.Filing is null || context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        return View("MerchantTaxRegistration_Filing_SummaryAdmin", registration)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:10")
            .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:10");
    }

    public override IDisplayResult Edit(MerchantTaxRegistration registration, BuildEditorContext context)
        => Initialize<MerchantTaxRegistrationFilingViewModel>("MerchantTaxRegistrationFiling_Edit", async model =>
        {
            var filing = registration.Filing ?? new TaxFilingSchedule();
            var forms = await _returns.GetFormsAsync();
            var chosen = await _returns.GetFormsAsync(includeDisabled: true);

            model.Frequency = registration.Filing?.Frequency;
            model.DueMonthsAfterPeriod = filing.DueMonthsAfterPeriod;
            model.DueDay = filing.DueDay;
            model.MoveToNextBusinessDay = filing.MoveToNextBusinessDay;
            // No form named is the generic one; a form is chosen by its own name, whatever case the schedule kept.
            model.Form = chosen.TryGetValue(filing.Form ?? TaxationConstants.GenericTaxReturnFormName, out var form) ? form.Name : filing.Form;
            model.AnnualForm = filing.AnnualForm is not null && chosen.TryGetValue(filing.AnnualForm, out var annualForm) ? annualForm.Name : filing.AnnualForm;
            model.AnnualDueMonthsAfterYear = filing.AnnualDueMonthsAfterYear;
            model.AnnualDueDay = filing.AnnualDueDay;
            model.Frequencies =
            [
                new SelectListItem(S["Files no return here"], string.Empty),
                new SelectListItem(S["Monthly"], TaxFilingFrequencies.Monthly),
                new SelectListItem(S["Quarterly"], TaxFilingFrequencies.Quarterly),
                new SelectListItem(S["Annually"], TaxFilingFrequencies.Annual),
            ];
            model.Forms =
            [
                .. forms.Values
                    .Concat(chosen.Values.Where(candidate => candidate.Name == model.Form || candidate.Name == model.AnnualForm))
                    .DistinctBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(candidate => new SelectListItem(_localizerFactory.Localize(candidate.DisplayName).Value, candidate.Name))
                    .OrderBy(item => item.Text, StringComparer.CurrentCultureIgnoreCase),
            ];
        }).Location(l => l.Zone("Content", "5").Card(S["Returns"], "30"));

    public override async Task<IDisplayResult> UpdateAsync(MerchantTaxRegistration registration, UpdateEditorContext context)
    {
        var model = new MerchantTaxRegistrationFilingViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Frequency))
        {
            registration.Filing = null;

            return Edit(registration, context);
        }

        var forms = await _returns.GetFormsAsync(includeDisabled: true);

        // The generic form is kept as no form named, so the returns prepared before keep finding it.
        string NameOf(string name)
            => !string.IsNullOrEmpty(name) && forms.TryGetValue(name, out var form) ? form.Name : null;

        var periodicForm = NameOf(model.Form);

        registration.Filing = new TaxFilingSchedule
        {
            Frequency = model.Frequency,
            DueMonthsAfterPeriod = model.DueMonthsAfterPeriod,
            DueDay = model.DueDay,
            MoveToNextBusinessDay = model.MoveToNextBusinessDay,
            Form = string.Equals(periodicForm, TaxationConstants.GenericTaxReturnFormName, StringComparison.OrdinalIgnoreCase) ? null : periodicForm,
            AnnualForm = NameOf(model.AnnualForm),
            AnnualDueMonthsAfterYear = model.AnnualDueMonthsAfterYear,
            AnnualDueDay = model.AnnualDueDay,
        };

        return Edit(registration, context);
    }
}

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
        => Initialize<MerchantTaxRegistrationFilingViewModel>("MerchantTaxRegistrationFiling_Edit", model =>
        {
            var filing = registration.Filing ?? new TaxFilingSchedule();

            model.Frequency = registration.Filing?.Frequency;
            model.DueMonthsAfterPeriod = filing.DueMonthsAfterPeriod;
            model.DueDay = filing.DueDay;
            model.MoveToNextBusinessDay = filing.MoveToNextBusinessDay;
            model.Form = filing.Form;
            model.AnnualForm = filing.AnnualForm;
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
                .. _returns.GetForms().Values
                    .Select(form => new SelectListItem(_localizerFactory.Localize(form.DisplayName).Value, form.Name))
                    .OrderBy(item => item.Text, StringComparer.CurrentCultureIgnoreCase),
            ];
        }).Location("Content:5");

    public override async Task<IDisplayResult> UpdateAsync(MerchantTaxRegistration registration, UpdateEditorContext context)
    {
        var model = new MerchantTaxRegistrationFilingViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Frequency))
        {
            registration.Filing = null;

            return Edit(registration, context);
        }

        var forms = _returns.GetForms();

        registration.Filing = new TaxFilingSchedule
        {
            Frequency = model.Frequency,
            DueMonthsAfterPeriod = model.DueMonthsAfterPeriod,
            DueDay = model.DueDay,
            MoveToNextBusinessDay = model.MoveToNextBusinessDay,
            Form = !string.IsNullOrEmpty(model.Form) && forms.ContainsKey(model.Form) ? model.Form : null,
            AnnualForm = !string.IsNullOrEmpty(model.AnnualForm) && forms.ContainsKey(model.AnnualForm) ? model.AnnualForm : null,
            AnnualDueMonthsAfterYear = model.AnnualDueMonthsAfterYear,
            AnnualDueDay = model.AnnualDueDay,
        };

        return Edit(registration, context);
    }
}

using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Core.Canada;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The employer's answers for the Canadian slips that no pay statement holds, with Canada Payroll Filings on: the
/// dental benefits code of the T4's box 45.
/// </summary>
internal sealed class EmployerCanadaDisplayDriver : DisplayDriver<Employer>
{
    internal readonly IStringLocalizer S;

    public EmployerCanadaDisplayDriver(IStringLocalizer<EmployerCanadaDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(Employer employer, BuildEditorContext context)
        => Initialize<EmployerCanadaViewModel>("EmployerCanada_Edit", model =>
        {
            model.DentalBenefitsCode = employer.GetCanadaSettings().DentalBenefitsCode;
        }).Location("Content:6");

    public override async Task<IDisplayResult> UpdateAsync(Employer employer, UpdateEditorContext context)
    {
        var model = new EmployerCanadaViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.DentalBenefitsCode is not null && !CanadaEmployerSettings.IsDentalBenefitsCode(model.DentalBenefitsCode))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DentalBenefitsCode), S["Choose one of the five dental benefits codes."]);
        }
        else
        {
            var settings = employer.GetCanadaSettings();

            settings.DentalBenefitsCode = model.DentalBenefitsCode;
            employer.Put(settings);
        }

        return Edit(employer, context);
    }
}

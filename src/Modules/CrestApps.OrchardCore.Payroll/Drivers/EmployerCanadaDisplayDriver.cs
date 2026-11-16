using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Core.Canada;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The employer's answers for the Canadian slips that no pay statement holds, with Canada Payroll Filings on: the
/// dental benefits code of the T4's box 45.
/// </summary>
internal sealed class EmployerCanadaDisplayDriver : DisplayDriver<Employer>
{
    public override IDisplayResult Edit(Employer employer, BuildEditorContext context)
        => Initialize<EmployerCanadaViewModel>("EmployerCanada_Edit", model =>
        {
            model.DentalBenefitsCode = employer.GetCanadaSettings().DentalBenefitsCode;
        }).Location("Content:6");

    public override async Task<IDisplayResult> UpdateAsync(Employer employer, UpdateEditorContext context)
    {
        var model = new EmployerCanadaViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // Which codes are allowed is EmployerHandler's rule, as for a recipe.
        var settings = employer.GetCanadaSettings();

        settings.DentalBenefitsCode = model.DentalBenefitsCode;
        employer.Put(settings);

        return Edit(employer, context);
    }
}

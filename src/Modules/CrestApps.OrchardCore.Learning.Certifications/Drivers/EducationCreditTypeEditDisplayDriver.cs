using CrestApps.OrchardCore.Learning.Certifications.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Certifications.Drivers;

/// <summary>
/// The editor of an education credit type: one card for the type and one for external learning.
/// </summary>
internal sealed class EducationCreditTypeEditDisplayDriver : DisplayDriver<EducationCreditTypeEditViewModel>
{
    internal readonly IStringLocalizer S;

    public EducationCreditTypeEditDisplayDriver(IStringLocalizer<EducationCreditTypeEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(EducationCreditTypeEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<EducationCreditTypeEditViewModel>("EducationCreditTypeGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Credit Type"], "10")),
            Initialize<EducationCreditTypeEditViewModel>("EducationCreditTypeExternalLearning_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["External Learning"], "20")));
    }

    public override async Task<IDisplayResult> UpdateAsync(EducationCreditTypeEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Name,
            m => m.Unit,
            m => m.Description,
            m => m.AllowExternalLearning);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates.
    protected override void BuildPrefix(EducationCreditTypeEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(EducationCreditTypeEditViewModel source, EducationCreditTypeEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.Unit = source.Unit;
        model.Description = source.Description;
        model.AllowExternalLearning = source.AllowExternalLearning;
    }
}

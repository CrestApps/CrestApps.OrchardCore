using CrestApps.OrchardCore.Skills.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Skills.Drivers;

/// <summary>
/// The card of the skill editor.
/// </summary>
internal sealed class SkillEditDisplayDriver : DisplayDriver<SkillEditViewModel>
{
    internal readonly IStringLocalizer S;

    public SkillEditDisplayDriver(IStringLocalizer<SkillEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SkillEditViewModel model, BuildEditorContext context)
    {
        return Initialize<SkillEditViewModel>("SkillFields_Edit", m => Populate(model, m))
            .Location(l => l.Zone("Content", "1").Card(S["Skill"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(SkillEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.Category, m => m.Description, m => m.RequiredCertificationId, m => m.Enabled);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(SkillEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(SkillEditViewModel source, SkillEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.Category = source.Category;
        model.Description = source.Description;
        model.RequiredCertificationId = source.RequiredCertificationId;
        model.Enabled = source.Enabled;
        model.CertificationOptions = source.CertificationOptions;
    }
}

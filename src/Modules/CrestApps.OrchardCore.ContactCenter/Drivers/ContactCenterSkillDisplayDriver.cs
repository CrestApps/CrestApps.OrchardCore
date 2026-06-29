using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

internal sealed class ContactCenterSkillDisplayDriver : DisplayDriver<ContactCenterSkill>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSkillDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterSkillDisplayDriver(IStringLocalizer<ContactCenterSkillDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(ContactCenterSkill skill, BuildDisplayContext context)
    {
        return CombineAsync(
            View("ContactCenterSkill_Fields_SummaryAdmin", skill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("ContactCenterSkill_Buttons_SummaryAdmin", skill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("ContactCenterSkill_DefaultMeta_SummaryAdmin", skill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ContactCenterSkill skill, BuildEditorContext context)
    {
        return Initialize<ContactCenterSkillViewModel>("ContactCenterSkillFields_Edit", model =>
        {
            model.Id = skill.ItemId;
            model.Name = skill.Name;
            model.Description = skill.Description;
            model.Enabled = skill.Enabled;
        }).Location("Content:1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ContactCenterSkill skill, UpdateEditorContext context)
    {
        var model = new ContactCenterSkillViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Name is a required field."]);
        }

        skill.Name = model.Name?.Trim();
        skill.Description = model.Description?.Trim();
        skill.Enabled = model.Enabled;

        return Edit(skill, context);
    }
}

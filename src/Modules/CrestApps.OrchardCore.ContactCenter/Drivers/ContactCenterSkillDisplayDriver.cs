using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

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
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = skill.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("ContactCenterSkill_Fields_SummaryAdmin", skill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ContactCenterSkill_Description_SummaryAdmin", skill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrWhiteSpace(skill.Description))),
            // A skill has no facts beyond being turned off, so in the grid the badge sits under its name.
            View("ContactCenterSkill_DefaultTags_SummaryAdmin", skill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:10")
                .RenderWhen(() => Task.FromResult(!skill.Enabled)),
            View("ContactCenterSkill_Buttons_SummaryAdmin", skill).Location("Actions:5"),
            View("ContactCenterSkill_DefaultMeta_SummaryAdmin", skill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5")
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
        }).Location("Content:1%General;1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ContactCenterSkill skill, UpdateEditorContext context)
    {
        var model = new ContactCenterSkillViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);


        skill.Name = model.Name?.Trim();
        skill.Description = model.Description?.Trim();
        skill.Enabled = model.Enabled;

        return Edit(skill, context);
    }
}

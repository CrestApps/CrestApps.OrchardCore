using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.ViewModels;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Learning.Certifications.Drivers;

/// <summary>
/// Edits the competencies a course or a learning path grants on completion (Skills skills, listed by their category,
/// which is the competency framework), and shows them on its page.
/// </summary>
internal sealed class CompetenciesPartDisplayDriver : ContentPartDisplayDriver<CompetenciesPart>
{
    private readonly ISkillManager _skillManager;

    internal readonly IStringLocalizer S;

    public CompetenciesPartDisplayDriver(ISkillManager skillManager, IStringLocalizer<CompetenciesPartDisplayDriver> stringLocalizer)
    {
        _skillManager = skillManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(CompetenciesPart part, BuildPartDisplayContext context)
    {
        if (part.SkillIds.Count == 0)
        {
            return null;
        }

        return Initialize<CompetenciesDisplayViewModel>("CompetenciesPart", async model =>
        {
            var skills = await SkillsByIdAsync();

            model.Names = part.SkillIds
                .Where(skills.ContainsKey)
                .Select(id => skills[id])
                .Where(skill => skill.Enabled)
                .Select(skill => skill.Name)
                .ToArray();
            model.Proficiency = part.Proficiency;
        }).Location(OrchardCoreConstants.DisplayType.Detail, "Content:4.5");
    }

    public override IDisplayResult Edit(CompetenciesPart part, BuildPartEditorContext context)
        => Initialize<CompetenciesPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.SkillIds = [.. part.SkillIds];
            model.Proficiency = SkillProficiency.IsValid(part.Proficiency) ? part.Proficiency : CompetenciesPart.DefaultProficiency;
            model.Skills = await OptionsAsync(part.SkillIds);
        });

    public override async Task<IDisplayResult> UpdateAsync(CompetenciesPart part, UpdatePartEditorContext context)
    {
        var model = new CompetenciesPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, value => value.SkillIds, value => value.Proficiency);

        var chosen = (model.SkillIds ?? []).Where(id => !string.IsNullOrEmpty(id)).ToArray();

        // The card posts no level when the site has no skills yet, and the level only matters once a competency is
        // chosen: an empty one keeps the default, so a course can always be saved.
        if (model.Proficiency == 0)
        {
            model.Proficiency = SkillProficiency.IsValid(part.Proficiency) ? part.Proficiency : CompetenciesPart.DefaultProficiency;
        }

        if (chosen.Length > 0 && !SkillProficiency.IsValid(model.Proficiency))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Proficiency), S["Choose a level from 1 to 5."]);

            return Edit(part, context);
        }

        var skills = chosen.Length == 0 ? new Dictionary<string, Skill>(StringComparer.Ordinal) : await SkillsByIdAsync();

        part.SkillIds = chosen
            .Where(skills.ContainsKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        part.Proficiency = SkillProficiency.IsValid(model.Proficiency) ? model.Proficiency : CompetenciesPart.DefaultProficiency;

        return Edit(part, context);
    }

    private async Task<Dictionary<string, Skill>> SkillsByIdAsync()
        => (await _skillManager.GetAllAsync()).ToDictionary(skill => skill.ItemId, StringComparer.Ordinal);

    private async Task<IList<SelectListItem>> OptionsAsync(IReadOnlyCollection<string> selected)
    {
        var chosen = new HashSet<string>(selected ?? [], StringComparer.Ordinal);
        var groups = new Dictionary<string, SelectListGroup>(StringComparer.OrdinalIgnoreCase);
        var noFramework = S["No framework"].Value;

        // A disabled skill can't be granted; it stays listed only while the item still names it.
        return (await _skillManager.GetAllAsync())
            .Where(skill => skill.Enabled || chosen.Contains(skill.ItemId))
            .OrderBy(skill => skill.Category ?? noFramework, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(skill => skill.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(skill =>
            {
                var category = string.IsNullOrEmpty(skill.Category) ? noFramework : skill.Category;

                if (!groups.TryGetValue(category, out var group))
                {
                    groups[category] = group = new SelectListGroup { Name = category };
                }

                return new SelectListItem(skill.Enabled ? skill.Name : S["{0} (disabled)", skill.Name].Value, skill.ItemId, chosen.Contains(skill.ItemId))
                {
                    Group = group,
                };
            })
            .ToList();
    }
}

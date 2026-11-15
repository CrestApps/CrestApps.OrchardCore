using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Certifications.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Learning.Certifications.Drivers;

/// <summary>
/// Edits the education credits a course or path awards, and shows them on its page.
/// </summary>
internal sealed class EducationCreditsPartDisplayDriver : ContentPartDisplayDriver<EducationCreditsPart>
{
    private readonly IEducationCreditService _credits;

    internal readonly IStringLocalizer S;

    public EducationCreditsPartDisplayDriver(IEducationCreditService credits, IStringLocalizer<EducationCreditsPartDisplayDriver> stringLocalizer)
    {
        _credits = credits;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(EducationCreditsPart part, BuildPartDisplayContext context)
    {
        if (part.Awards.Count == 0)
        {
            return null;
        }

        return Initialize<EducationCreditsDisplayViewModel>("EducationCreditsPart", async model =>
        {
            var types = (await _credits.ListTypesAsync()).ToDictionary(type => type.ItemId, StringComparer.Ordinal);

            model.Awards = part.Awards
                .Where(award => types.ContainsKey(award.CreditTypeId))
                .Select(award => (types[award.CreditTypeId].Name, types[award.CreditTypeId].Unit, award.Amount))
                .ToArray();
        }).Location(OrchardCoreConstants.DisplayType.Detail, "Content:4");
    }

    public override IDisplayResult Edit(EducationCreditsPart part, BuildPartEditorContext context)
        => Initialize<EducationCreditsPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.Awards = part.Awards.Select(award => new EducationCreditAwardRow { CreditTypeId = award.CreditTypeId, Amount = award.Amount, ValidityMonths = award.ValidityMonths }).ToList();
            model.Types = (await _credits.ListTypesAsync()).Select(type => new SelectListItem(type.Name, type.ItemId)).ToList();
        });

    public override async Task<IDisplayResult> UpdateAsync(EducationCreditsPart part, UpdatePartEditorContext context)
    {
        var model = new EducationCreditsPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, value => value.Awards);

        var awards = new List<EducationCreditAward>();

        foreach (var row in model.Awards ?? [])
        {
            if (string.IsNullOrEmpty(row.CreditTypeId))
            {
                continue;
            }

            if (row.Amount is not > 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Awards), S["The credits awarded must be more than zero."]);

                continue;
            }

            awards.Add(new EducationCreditAward { CreditTypeId = row.CreditTypeId, Amount = row.Amount.Value, ValidityMonths = row.ValidityMonths is > 0 ? row.ValidityMonths : null });
        }

        part.Awards = awards.GroupBy(award => award.CreditTypeId, StringComparer.Ordinal).Select(group => group.Last()).ToList();

        return Edit(part, context);
    }
}

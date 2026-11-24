using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Coursework.Drivers;

/// <summary>
/// The editor of a rubric: criteria with a title and a description, each with levels of a title and points. Rows
/// without a title are left out; ids are kept so grades that chose a level still find it.
/// </summary>
internal sealed class RubricPartDisplayDriver : ContentPartDisplayDriver<RubricPart>
{
    internal readonly IStringLocalizer S;

    public RubricPartDisplayDriver(IStringLocalizer<RubricPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(RubricPart part, BuildPartEditorContext context)
        => Initialize<RubricPartViewModel>(GetEditorShapeType(context), model =>
        {
            model.Criteria = (part.Criteria ?? [])
                .Select(criterion => new RubricCriterionRow
                {
                    Id = criterion.Id,
                    Title = criterion.Title,
                    Description = criterion.Description,
                    Levels = (criterion.Levels ?? []).Select(level => new RubricLevelRow { Id = level.Id, Title = level.Title, Points = level.Points }).ToList(),
                })
                .ToList();
        });

    public override async Task<IDisplayResult> UpdateAsync(RubricPart part, UpdatePartEditorContext context)
    {
        var model = new RubricPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var criteria = new List<RubricCriterion>();

        foreach (var row in model.Criteria ?? [])
        {
            if (string.IsNullOrWhiteSpace(row?.Title))
            {
                continue;
            }

            var levels = (row.Levels ?? [])
                .Where(level => !string.IsNullOrWhiteSpace(level?.Title))
                .Select(level => new RubricLevel
                {
                    Id = string.IsNullOrWhiteSpace(level.Id) ? IdGenerator.GenerateId() : level.Id.Trim(),
                    Title = level.Title.Trim(),
                    Points = level.Points ?? 0,
                })
                .ToList();

            if (levels.Count == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Criteria), S["Give the criterion '{0}' at least one level.", row.Title.Trim()]);
            }

            if (levels.Any(level => level.Points < 0))
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Criteria), S["The levels of '{0}' cannot be worth less than zero points.", row.Title.Trim()]);
            }

            criteria.Add(new RubricCriterion
            {
                Id = string.IsNullOrWhiteSpace(row.Id) ? IdGenerator.GenerateId() : row.Id.Trim(),
                Title = row.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(row.Description) ? null : row.Description.Trim(),
                Levels = levels,
            });
        }

        if (criteria.Count == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Criteria), S["Add at least one criterion with a title."]);
        }

        // The ids come back from the form; grades keep the level chosen for each criterion by its id, so two criteria
        // with one id could not be told apart.
        if (criteria.GroupBy(criterion => criterion.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Criteria), S["Two criteria have the same id. Reload the editor and add the criterion again."]);
        }

        part.Criteria = criteria;

        return Edit(part, context);
    }
}

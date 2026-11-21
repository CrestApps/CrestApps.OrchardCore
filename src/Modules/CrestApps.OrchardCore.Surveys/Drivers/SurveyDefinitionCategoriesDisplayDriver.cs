using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Edits the score categories and bands of a survey definition.
/// </summary>
internal sealed class SurveyDefinitionCategoriesDisplayDriver : DisplayDriver<SurveyDefinition>
{
    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyDefinition definition, BuildEditorContext context)
        => Initialize<SurveyCategoriesEditViewModel>("SurveyDefinitionCategories_Edit", model =>
        {
            model.Categories = definition.ScoreCategories.ToList();
            model.Bands = definition.ScoreBands.OrderByDescending(band => band.MinPercent).ToList();
        }).Location("Content:1#Scoring");

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyDefinition definition, UpdateEditorContext context)
    {
        var model = new SurveyCategoriesEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        definition.ScoreCategories = model.Categories
            .Where(category => category is not null && !string.IsNullOrWhiteSpace(category.Title))
            .Select((category, index) => new ScoreCategory
            {
                Id = string.IsNullOrWhiteSpace(category.Id) ? IdGenerator.GenerateId() : category.Id,
                Name = SurveyNames.IsValidVariableCode(category.Name?.Trim()) ? category.Name.Trim() : $"category_{index + 1}",
                Title = category.Title.Trim(),
            })
            .ToList();
        definition.ScoreBands = model.Bands
            .Where(band => band is not null && !string.IsNullOrWhiteSpace(band.Label))
            .Select(band => new ScoreBand
            {
                Id = string.IsNullOrWhiteSpace(band.Id) ? IdGenerator.GenerateId() : band.Id,
                MinPercent = Math.Clamp(band.MinPercent, 0, 100),
                Label = band.Label.Trim(),
                Message = band.Message?.Trim(),
                Passing = band.Passing,
            })
            .OrderByDescending(band => band.MinPercent)
            .ToList();

        return Edit(definition, context);
    }
}

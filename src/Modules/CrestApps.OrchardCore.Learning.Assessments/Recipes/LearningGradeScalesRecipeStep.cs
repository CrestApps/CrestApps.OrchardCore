using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Learning.Assessments.Recipes;

/// <summary>
/// Imports grade scales. A scale keeps its id, so a second import updates it.
/// </summary>
internal sealed class LearningGradeScalesRecipeStep : NamedRecipeStepHandler
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly GradeScaleStore _store;

    internal readonly IStringLocalizer S;

    public LearningGradeScalesRecipeStep(GradeScaleStore store, IStringLocalizer<LearningGradeScalesRecipeStep> stringLocalizer)
        : base(AssessmentsConstants.GradeScalesRecipeStep)
    {
        _store = store;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step["GradeScales"] is not JsonArray rows)
        {
            return;
        }

        foreach (var row in rows.OfType<JsonObject>())
        {
            GradeScale incoming;

            try
            {
                incoming = row.Deserialize<GradeScale>(JsonOptions);
            }
            catch (JsonException exception)
            {
                context.Errors.Add(S["A grade scale could not be read: {0}", exception.Message]);

                continue;
            }

            if (incoming is null || string.IsNullOrWhiteSpace(incoming.Name) || incoming.Bands.Count == 0)
            {
                context.Errors.Add(S["A grade scale has no name or no bands; it was skipped."]);

                continue;
            }

            var existing = string.IsNullOrEmpty(incoming.ItemId) ? null : await _store.FindByIdAsync(incoming.ItemId);

            if (existing is null)
            {
                await _store.CreateAsync(incoming);

                continue;
            }

            // Copy onto the instance the session tracks; saving the deserialized copy would store a second document.
            existing.Name = incoming.Name;
            existing.Bands = incoming.Bands;
            await _store.UpdateAsync(existing);
        }
    }
}

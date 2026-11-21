using CrestApps.Core;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Imports survey templates, matched by id, then by name.
/// </summary>
internal sealed class SurveyTemplatesRecipeStep : NamedRecipeStepHandler
{
    private readonly SurveyTemplateStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyTemplatesRecipeStep"/> class.
    /// </summary>
    /// <param name="store">The template store.</param>
    /// <param name="clock">The clock.</param>
    public SurveyTemplatesRecipeStep(
        SurveyTemplateStore store,
        IClock clock)
        : base(SurveysRecipeSteps.Templates)
    {
        _store = store;
        _clock = clock;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var token in SurveysRecipeJson.Array(context.Step, SurveysRecipeSteps.TemplatesSection))
        {
            var id = SurveysRecipeJson.String(token, nameof(SurveyTemplate.ItemId));
            var name = SurveysRecipeJson.String(token, nameof(SurveyTemplate.Name))?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                context.Errors.Add("A survey template has no name.");

                continue;
            }

            var existing = (string.IsNullOrEmpty(id) ? null : await _store.FindByIdAsync(id))
                ?? await _store.FindByNameAsync(name);

            if (existing is not null)
            {
                SurveysRecipeJson.Populate(existing, token);
                existing.Name = name;
                existing.ModifiedUtc = _clock.UtcNow;
                existing.Definition ??= new SurveyDefinition();
                await _store.UpdateAsync(existing);

                continue;
            }

            var entry = SurveysRecipeJson.Read<SurveyTemplate>(token);
            entry.ItemId = !string.IsNullOrEmpty(id) && UniqueId.IsValid(id) ? id : IdGenerator.GenerateId();
            entry.Name = name;
            entry.Definition ??= new SurveyDefinition();

            if (entry.CreatedUtc == default)
            {
                entry.CreatedUtc = _clock.UtcNow;
            }

            await _store.CreateAsync(entry);
        }
    }
}

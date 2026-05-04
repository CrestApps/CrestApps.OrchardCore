using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

internal sealed class AIProfileTemplateStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The recipe step key used to identify this handler.
    /// </summary>
    public const string StepKey = "AIProfileTemplate";

    private readonly INamedCatalogManager<AIProfileTemplate> _templateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateStep"/> class.
    /// </summary>
    /// <param name="templateManager">The AI profile template manager.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public AIProfileTemplateStep(
        INamedCatalogManager<AIProfileTemplate> templateManager,
        IStringLocalizer<AIProfileTemplateStep> stringLocalizer)
        : base(StepKey)
    {
        _templateManager = templateManager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<AIProfileTemplateStepModel>();
        var tokens = model.Templates.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            AIProfileTemplate template = null;

            var id = token[nameof(AIProfileTemplate.ItemId)]?.GetValue<string>();

            var hasId = !string.IsNullOrEmpty(id);

            if (hasId)
            {
                template = await _templateManager.FindByIdAsync(id);
            }

            if (template is null)
            {
                var name = token[nameof(AIProfileTemplate.Name)]?.GetValue<string>()?.Trim();

                if (!string.IsNullOrEmpty(name))
                {
                    template = await _templateManager.FindByNameAsync(name);
                }
            }

            if (template is not null)
            {
                await _templateManager.UpdateAsync(template, token);
            }
            else
            {
                template = await _templateManager.NewAsync(token);

                if (hasId && UniqueId.IsValid(id))
                {
                    template.ItemId = id;
                }
            }

            var validationResult = await _templateManager.ValidateAsync(template);

            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            await _templateManager.CreateAsync(template);
        }
    }

    private sealed class AIProfileTemplateStepModel
    {
        /// <summary>
        /// Gets or sets the collection of AI profile template definitions to import.
        /// </summary>
        public JsonArray Templates { get; set; }
    }
}

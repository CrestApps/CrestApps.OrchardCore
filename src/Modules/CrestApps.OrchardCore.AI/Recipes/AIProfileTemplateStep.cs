using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

internal sealed class AIProfileTemplateStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The recipe step key used to identify this handler.
    /// </summary>
    public const string StepKey = "AIProfileTemplate";

    private readonly INamedSourceCatalogManager<AIProfileTemplate> _templateManager;
    private readonly AIOptions _aiOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateStep"/> class.
    /// </summary>
    /// <param name="templateManager">The AI profile template manager.</param>
    /// <param name="aiOptions">The AI configuration options.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public AIProfileTemplateStep(
        INamedSourceCatalogManager<AIProfileTemplate> templateManager,
        IOptions<AIOptions> aiOptions,
        IStringLocalizer<AIProfileTemplateStep> stringLocalizer)
        : base(StepKey)
    {
        _templateManager = templateManager;
        _aiOptions = aiOptions.Value;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<AIProfileTemplateStepModel>();
        var tokens = model.Templates.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            AIProfileTemplate template = null;
            var isNew = false;

            var id = token[nameof(AIProfileTemplate.ItemId)]?.GetValue<string>();
            var source = token[nameof(AIProfileTemplate.Source)]?.GetValue<string>()?.Trim();

            var hasId = !string.IsNullOrEmpty(id);

            if (hasId)
            {
                template = await _templateManager.FindByIdAsync(id);
            }

            var name = token[nameof(AIProfileTemplate.Name)]?.GetValue<string>()?.Trim();
            var hasSource = !string.IsNullOrEmpty(source);

            if (template is null)
            {
                if (!hasSource)
                {
                    context.Errors.Add(S["Could not find template source. The template will not be imported."]);

                    continue;
                }

                if (string.IsNullOrEmpty(name))
                {
                    context.Errors.Add(S["Could not find template name. The template will not be imported."]);

                    continue;
                }

                template = await _templateManager.GetAsync(name, source);
            }

            if (template is not null)
            {
                await _templateManager.UpdateAsync(template, token);
            }
            else
            {
                isNew = true;
                if (!_aiOptions.TemplateSources.TryGetValue(source, out _))
                {
                    context.Errors.Add(S["Unable to find a template-source that can handle the source '{0}'.", source]);

                    return;
                }

                template = await _templateManager.NewAsync(name, source, token);

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

            if (isNew)
            {
                await _templateManager.CreateAsync(template);
            }
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

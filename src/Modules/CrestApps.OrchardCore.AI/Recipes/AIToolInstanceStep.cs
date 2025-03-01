using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

public sealed class AIToolInstanceStep : NamedRecipeStepHandler
{
    public const string StepKey = "AIToolInstance";

    private readonly IModelManager<AIToolInstance> _manager;

    internal readonly IStringLocalizer S;

    public AIToolInstanceStep(
        IModelManager<AIToolInstance> manager,
        IStringLocalizer<AIToolInstanceStep> stringLocalizer)
        : base(StepKey)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<AIToolInstanceStepModel>();
        var tokens = model.Instances.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            AIToolInstance instance = null;

            var id = token[nameof(AIProfile.Id)]?.GetValue<string>();

            if (!string.IsNullOrEmpty(id))
            {
                instance = await _manager.FindByIdAsync(id);
            }

            if (instance is not null)
            {
                await _manager.UpdateAsync(instance, token);
            }
            else
            {
                var sourceName = token[nameof(AIProfile.Source)]?.GetValue<string>();

                if (string.IsNullOrEmpty(sourceName))
                {
                    context.Errors.Add(S["Could not find tool-source value. The tool-instance will not be imported"]);

                    continue;
                }

                instance = await _manager.NewAsync(sourceName, token);

                if (instance == null)
                {
                    context.Errors.Add(S["Unable to find a tool-source that can handle the source '{Source}'.", sourceName]);

                    continue;
                }
            }

            var validationResult = await _manager.ValidateAsync(instance);

            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            await _manager.SaveAsync(instance);
        }
    }

    private sealed class AIToolInstanceStepModel
    {
        public JsonArray Instances { get; set; }
    }
}

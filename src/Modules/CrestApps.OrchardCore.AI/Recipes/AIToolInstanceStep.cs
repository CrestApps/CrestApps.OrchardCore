using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

internal sealed class AIToolInstanceStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The recipe step key used to identify this handler.
    /// </summary>
    public const string StepKey = "AIToolInstances";

    private readonly ISourceCatalogManager<AIToolInstance> _manager;
    private readonly INamedCatalog<AIToolInstance> _instancesCatalog;
    private readonly AIOptions _aiOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstanceStep"/> class.
    /// </summary>
    /// <param name="manager">The AI tool instance catalog manager.</param>
    /// <param name="instancesCatalog">The named catalog used to resolve instances by name.</param>
    /// <param name="aiOptions">The AI configuration options.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public AIToolInstanceStep(
        ISourceCatalogManager<AIToolInstance> manager,
        INamedCatalog<AIToolInstance> instancesCatalog,
        IOptions<AIOptions> aiOptions,
        IStringLocalizer<AIToolInstanceStep> stringLocalizer)
    : base(StepKey)
    {
        _manager = manager;
        _instancesCatalog = instancesCatalog;
        _aiOptions = aiOptions.Value;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<AIToolInstanceStepModel>();
        var tokens = model.Instances.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            AIToolInstance instance = null;
            var isNew = false;

            var id = token[nameof(AIToolInstance.ItemId)]?.GetValue<string>();

            var hasId = !string.IsNullOrEmpty(id);

            if (hasId)
            {
                instance = await _manager.FindByIdAsync(id);
            }

            var sourceName = token[nameof(AIToolInstance.Source)]?.GetValue<string>();
            var hasSource = !string.IsNullOrEmpty(sourceName);

            if (instance is null)
            {
                var name = token[nameof(AIToolInstance.Name)]?.GetValue<string>()?.Trim();

                if (!string.IsNullOrEmpty(name))
                {
                    instance = await _instancesCatalog.FindByNameAsync(name);
                }
            }

            if (instance is not null)
            {
                await _manager.UpdateAsync(instance, token);
            }
            else
            {
                isNew = true;

                if (!hasSource)
                {
                    context.Errors.Add(S["Could not find tool instance source value. The tool instance will not be imported."]);

                    continue;
                }

                if (!_aiOptions.ToolInstanceSources.TryGetValue(sourceName, out _))
                {
                    context.Errors.Add(S["Unable to find a tool instance source that can handle the source '{0}'.", sourceName]);

                    return;
                }

                instance = await _manager.NewAsync(sourceName, token);

                if (hasId && UniqueId.IsValid(id))
                {
                    instance.ItemId = id;
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

            if (isNew)
            {
                await _manager.CreateAsync(instance);
            }
        }
    }

    private sealed class AIToolInstanceStepModel
    {
        /// <summary>
        /// Gets or sets the collection of AI tool instance definitions to import.
        /// </summary>
        public JsonArray Instances { get; set; }
    }
}

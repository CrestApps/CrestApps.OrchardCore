using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

public sealed class AIProviderConnectionsStep : NamedRecipeStepHandler
{
    public const string StepKey = "AIProviderConnections";

    private readonly INamedModelManager<AIProviderConnection> _manager;
    private readonly AIOptions _aiOptions;

    internal readonly IStringLocalizer S;

    public AIProviderConnectionsStep(
        INamedModelManager<AIProviderConnection> manager,
        IOptions<AIOptions> aiOptions,
        IStringLocalizer<AIProfileStep> stringLocalizer)
        : base(StepKey)
    {
        _manager = manager;
        _aiOptions = aiOptions.Value;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<AIProviderConnectionStepModel>();
        var tokens = model.Connections.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            AIProviderConnection connection = null;

            var id = token[nameof(AIProviderConnection.Id)]?.GetValue<string>();

            if (!string.IsNullOrEmpty(id))
            {
                connection = await _manager.FindByIdAsync(id);
            }

            if (connection is null)
            {
                var name = token[nameof(AIProviderConnection.Name)]?.GetValue<string>()?.Trim();

                if (!string.IsNullOrEmpty(name))
                {
                    connection = await _manager.FindByNameAsync(name);
                }
            }

            if (connection is not null)
            {
                await _manager.UpdateAsync(connection, token);
            }
            else
            {
                var sourceName = token[nameof(AIProviderConnection.Source)]?.GetValue<string>();

                if (string.IsNullOrEmpty(sourceName))
                {
                    context.Errors.Add(S["Could not find profile-source value. The profile will not be imported"]);

                    continue;
                }

                if (!_aiOptions.ConnectionSources.TryGetValue(sourceName, out var entry))
                {
                    context.Errors.Add(S["Unable to find a tool-source that can handle the source '{0}'.", sourceName]);

                    return;
                }

                connection = await _manager.NewAsync(sourceName, token);
            }

            var validationResult = await _manager.ValidateAsync(connection);

            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            await _manager.SaveAsync(connection);
        }
    }

    private sealed class AIProviderConnectionStepModel
    {
        public JsonArray Connections { get; set; }
    }
}

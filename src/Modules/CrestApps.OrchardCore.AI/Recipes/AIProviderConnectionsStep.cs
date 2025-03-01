using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

public sealed class AIProviderConnectionsStep : NamedRecipeStepHandler
{
    public const string StepKey = "AIProviderConnections";

    private readonly INamedModelManager<AIProviderConnection> manager;
    private readonly AICompletionOptions _options;

    internal readonly IStringLocalizer S;

    public AIProviderConnectionsStep(
        INamedModelManager<AIProviderConnection> manager,
        IOptions<AICompletionOptions> options,
        ILogger<AIProfileStep> logger,
        IStringLocalizer<AIProfileStep> stringLocalizer)
        : base(StepKey)
    {
        this.manager = manager;
        _options = options.Value;
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
                connection = await manager.FindByIdAsync(id);
            }

            if (connection is null)
            {
                var name = token[nameof(AIProviderConnection.Name)]?.GetValue<string>()?.Trim();

                if (!string.IsNullOrEmpty(name))
                {
                    connection = await manager.FindByNameAsync(name);
                }
            }

            if (connection is not null)
            {
                await manager.UpdateAsync(connection, token);
            }
            else
            {
                var sourceName = token[nameof(AIProviderConnection.Source)]?.GetValue<string>();

                if (string.IsNullOrEmpty(sourceName))
                {
                    context.Errors.Add(S["Could not find profile-source value. The profile will not be imported"]);

                    continue;
                }

                if (!_options.ConnectionSources.TryGetValue(sourceName, out var entry))
                {
                    context.Errors.Add(S["Unable to find a tool-source that can handle the source '{0}'.", sourceName]);

                    return;
                }

                connection = await manager.NewAsync(sourceName, token);
            }

            var validationResult = await manager.ValidateAsync(connection);

            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            await manager.SaveAsync(connection);
        }
    }

    private sealed class AIProviderConnectionStepModel
    {
        public JsonArray Connections { get; set; }
    }
}

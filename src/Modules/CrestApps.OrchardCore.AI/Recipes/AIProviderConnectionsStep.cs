using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

internal sealed class AIProviderConnectionsStep : NamedRecipeStepHandler
{
    public const string StepKey = "AIProviderConnections";

    private readonly INamedSourceCatalogManager<AIProviderConnection> _manager;
    private readonly AIOptions _aiOptions;

    internal readonly IStringLocalizer S;

    public AIProviderConnectionsStep(
        INamedSourceCatalogManager<AIProviderConnection> manager,
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

            var id = token[nameof(AIProviderConnection.ItemId)]?.GetValue<string>();

            if (!string.IsNullOrEmpty(id))
            {
                connection = await _manager.FindByIdAsync(id);
            }

            var sourceName = token[nameof(AIProviderConnection.Source)]?.GetValue<string>();
            var hasSource = !string.IsNullOrEmpty(sourceName);

            if (connection is null)
            {
                if (!hasSource)
                {
                    context.Errors.Add(S["Could not find connection-source value. The profile will not be imported."]);

                    continue;
                }

                var name = token[nameof(AIProviderConnection.Name)]?.GetValue<string>()?.Trim();

                if (!string.IsNullOrEmpty(name))
                {
                    connection = await _manager.GetAsync(name, sourceName);
                }
            }

            if (connection is not null)
            {
                await _manager.UpdateAsync(connection, token);
            }
            else
            {
                if (!hasSource)
                {
                    context.Errors.Add(S["Could not find connection-source value. The profile will not be imported."]);

                    continue;
                }

                if (!_aiOptions.ConnectionSources.TryGetValue(sourceName, out _))
                {
                    context.Errors.Add(S["Unable to find a connection-source that can handle the source '{0}'.", sourceName]);

                    return;
                }

                connection = await _manager.NewAsync(sourceName, token);

                if (!string.IsNullOrEmpty(id) && IdValidator.IsValidId(id))
                {
                    connection.ItemId = id;
                }
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

            await _manager.CreateAsync(connection);
        }
    }

    private sealed class AIProviderConnectionStepModel
    {
        public JsonArray Connections { get; set; }
    }
}

using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

internal sealed class AIDataSourceStep : NamedRecipeStepHandler
{
    public const string StepKey = "AIDataSource";

    private readonly IAIDataSourceManager _dataManager;
    private readonly AIOptions _aiOptions;

    internal readonly IStringLocalizer S;

    public AIDataSourceStep(
        IAIDataSourceManager dataManager,
        IOptions<AIOptions> aiOptions,
        IStringLocalizer<AIProfileStep> stringLocalizer)
        : base(StepKey)
    {
        _dataManager = dataManager;
        _aiOptions = aiOptions.Value;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<AIDataSourcesStepModel>();
        var tokens = model.DataSources.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            AIDataSource dataSource = null;

            var id = token[nameof(AIDataSource.ItemId)]?.GetValue<string>();

            var hasId = !string.IsNullOrEmpty(id);

            if (hasId)
            {
                dataSource = await _dataManager.FindByIdAsync(id);
            }

            if (dataSource is not null)
            {
                await _dataManager.UpdateAsync(dataSource, token);
            }
            else
            {
                var profileSource = token[nameof(AIDataSource.ProfileSource)]?.GetValue<string>();

                if (string.IsNullOrEmpty(profileSource))
                {
                    context.Errors.Add(S["Could not find profile-source value. The data-source will not be imported"]);

                    continue;
                }

                var type = token[nameof(AIDataSource.Type)]?.GetValue<string>();

                if (string.IsNullOrEmpty(type))
                {
                    context.Errors.Add(S["Could not find type value. The data-source will not be imported"]);

                    continue;
                }

                if (!_aiOptions.DataSources.TryGetValue(new AIDataSourceKey(profileSource, type), out var _))
                {
                    context.Errors.Add(S["Unable to find a profile-source named '{0}' with the type '{1}'.", profileSource, type]);

                    return;
                }

                dataSource = await _dataManager.NewAsync(profileSource, type, token);

                if (hasId && IdValidator.IsValidId(id))
                {
                    dataSource.ItemId = id;
                }
            }

            var validationResult = await _dataManager.ValidateAsync(dataSource);

            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            await _dataManager.CreateAsync(dataSource);
        }
    }

    private sealed class AIDataSourcesStepModel
    {
        public JsonArray DataSources { get; set; }
    }
}

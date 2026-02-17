using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.DataSources.Recipes;

internal sealed class AIDataSourceStep : NamedRecipeStepHandler
{
    public const string StepKey = "AIDataSource";

    private readonly ICatalogManager<AIDataSource> _dataManager;

    internal readonly IStringLocalizer S;

    public AIDataSourceStep(
        ICatalogManager<AIDataSource> dataManager,
        IStringLocalizer<AIDataSourceStep> stringLocalizer)
        : base(StepKey)
    {
        _dataManager = dataManager;
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
                dataSource = await _dataManager.NewAsync(token);

                if (hasId && IdValidator.IsValid(id))
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

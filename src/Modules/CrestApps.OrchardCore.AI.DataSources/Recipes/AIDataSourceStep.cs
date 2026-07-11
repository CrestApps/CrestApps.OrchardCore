using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.DataSources.Recipes;

internal sealed class AIDataSourceStep : NamedRecipeStepHandler
{
    public const string StepKey = "AIDataSource";

    private readonly ISourceCatalogManager<AIDataSource> _dataManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceStep"/> class.
    /// </summary>
    /// <param name="dataManager">The data manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIDataSourceStep(
        ISourceCatalogManager<AIDataSource> dataManager,
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
            var isNew = false;

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
                isNew = true;
                dataSource = await _dataManager.NewAsync(token);

                if (hasId && UniqueId.IsValid(id))
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

            if (isNew)
            {
                await _dataManager.CreateAsync(dataSource);
            }
        }
    }

    private sealed class AIDataSourcesStepModel
    {
        /// <summary>
        /// Gets or sets the data sources.
        /// </summary>
        public JsonArray DataSources { get; set; }
    }
}

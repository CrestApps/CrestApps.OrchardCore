using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.DataSources.Recipes;

internal sealed class AIDataSourceStep : NamedRecipeStepHandler
{
    public const string StepKey = "AIDataSource";

    private readonly ISourceCatalogManager<AIDataSource> _dataManager;
    private readonly AIDataSourceSourceOptions _sourceOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceStep"/> class.
    /// </summary>
    /// <param name="dataManager">The data manager.</param>
    /// <param name="sourceOptions">The source options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIDataSourceStep(
        ISourceCatalogManager<AIDataSource> dataManager,
        IOptions<AIDataSourceSourceOptions> sourceOptions,
        IStringLocalizer<AIDataSourceStep> stringLocalizer)
    : base(StepKey)
    {
        _dataManager = dataManager;
        _sourceOptions = sourceOptions.Value;
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

            var source = token[nameof(AIDataSource.Source)]?.GetValue<string>();
            source = string.IsNullOrWhiteSpace(source)
                ? AIDataSourceSourceTypes.SearchIndexProfile
                : source;

            if (!TryGetSource(source))
            {
                context.Errors.Add(S["Invalid source used for AI data source '{0}'.", source]);

                continue;
            }

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
                dataSource = await _dataManager.NewAsync(source, token);

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

    private bool TryGetSource(string sourceType)
        => _sourceOptions.Sources.Any(entry =>
            string.Equals(entry.SourceType, sourceType, StringComparison.OrdinalIgnoreCase));

    private sealed class AIDataSourcesStepModel
    {
        /// <summary>
        /// Gets or sets the data sources.
        /// </summary>
        public JsonArray DataSources { get; set; }
    }
}

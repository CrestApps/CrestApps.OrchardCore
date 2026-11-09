using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Recipes;

/// <summary>
/// Imports AI data sources, matched by their item id. A data source without a source reads a search index profile.
/// </summary>
internal sealed class AIDataSourceStep : CatalogRecipeStepBase<AIDataSource>
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
        : base(StepKey, "DataSources")
    {
        _dataManager = dataManager;
        _sourceOptions = sourceOptions.Value;
        S = stringLocalizer;
    }

    protected override bool CanImport(JsonObject token, RecipeExecutionContext context)
    {
        var source = GetSource(token);

        if (_sourceOptions.Sources.Any(entry => string.Equals(entry.SourceType, source, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        context.Errors.Add(S["Invalid source used for AI data source '{0}'.", source]);

        return false;
    }

    protected override ValueTask<AIDataSource> FindByIdAsync(string id)
        => _dataManager.FindByIdAsync(id);

    protected override ValueTask UpdateAsync(AIDataSource entry, JsonObject token)
        => _dataManager.UpdateAsync(entry, token);

    protected override ValueTask<AIDataSource> NewAsync(JsonObject token, RecipeExecutionContext context)
        => _dataManager.NewAsync(GetSource(token), token);

    protected override ValueTask<ValidationResultDetails> ValidateAsync(AIDataSource entry)
        => _dataManager.ValidateAsync(entry);

    protected override ValueTask CreateAsync(AIDataSource entry)
        => _dataManager.CreateAsync(entry);

    private static string GetSource(JsonObject token)
    {
        var source = token[nameof(AIDataSource.Source)]?.GetValue<string>();

        return string.IsNullOrWhiteSpace(source)
            ? AIDataSourceSourceTypes.SearchIndexProfile
            : source;
    }
}

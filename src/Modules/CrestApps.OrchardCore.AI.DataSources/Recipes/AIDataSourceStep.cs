using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Recipes;
using CrestApps.OrchardCore.Core.Recipes;
using Microsoft.AspNetCore.DataProtection;
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
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IngestionSourceSecretOptions _secretOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceStep"/> class.
    /// </summary>
    /// <param name="dataManager">The data manager.</param>
    /// <param name="sourceOptions">The source options.</param>
    /// <param name="dataProtectionProvider">The data protection provider a secret given in clear text is protected with.</param>
    /// <param name="secretOptions">The purposes the secrets of each source's settings are protected with.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIDataSourceStep(
        ISourceCatalogManager<AIDataSource> dataManager,
        IOptions<AIDataSourceSourceOptions> sourceOptions,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<IngestionSourceSecretOptions> secretOptions,
        IStringLocalizer<AIDataSourceStep> stringLocalizer)
        : base(StepKey, "DataSources")
    {
        _dataManager = dataManager;
        _sourceOptions = sourceOptions.Value;
        _dataProtectionProvider = dataProtectionProvider;
        _secretOptions = secretOptions.Value;
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
    {
        ProtectSecrets(token, RecipeSecretDestinations.Snapshot(entry));

        // A recipe that leaves a secret out keeps the stored one; when it also changes where the entry sends it, the
        // kept secret is dropped, as the editor drops it.
        return RecipeSecretDestinations.UpdateAsync(entry, token, data => _dataManager.UpdateAsync(entry, data));
    }

    protected override ValueTask<AIDataSource> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        ProtectSecrets(token, stored: null);

        return _dataManager.NewAsync(GetSource(token), token);
    }

    // A secret the recipe gives in clear text is protected, as the editor protects it; one that is already protected (a
    // recipe made on this site) is kept as it is.
    private void ProtectSecrets(JsonObject token, JsonObject stored)
        => IngestionSourceSecretProtection.Protect(
            typeof(AIDataSource),
            token[nameof(AIDataSource.Properties)] as JsonObject,
            stored?[nameof(AIDataSource.Properties)] as JsonObject,
            _secretOptions,
            _dataProtectionProvider,
            refuseProtected: false);

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

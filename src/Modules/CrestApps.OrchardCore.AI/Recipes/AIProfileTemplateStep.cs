using System.Text.Json.Nodes;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.AI.Recipes;

/// <summary>
/// Imports AI profile templates. A template is matched by its item id, then by its name and source.
/// </summary>
internal sealed class AIProfileTemplateStep : CatalogRecipeStepBase<AIProfileTemplate>
{
    /// <summary>
    /// The recipe step key used to identify this handler.
    /// </summary>
    public const string StepKey = "AIProfileTemplate";

    private readonly INamedSourceCatalogManager<AIProfileTemplate> _templateManager;
    private readonly AIOptions _aiOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateStep"/> class.
    /// </summary>
    /// <param name="templateManager">The AI profile template manager.</param>
    /// <param name="aiOptions">The AI configuration options.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public AIProfileTemplateStep(
        INamedSourceCatalogManager<AIProfileTemplate> templateManager,
        IOptions<AIOptions> aiOptions,
        IStringLocalizer<AIProfileTemplateStep> stringLocalizer)
        : base(StepKey, "Templates")
    {
        _templateManager = templateManager;
        _aiOptions = aiOptions.Value;
        S = stringLocalizer;
    }

    protected override ValueTask<AIProfileTemplate> FindByIdAsync(string id)
        => _templateManager.FindByIdAsync(id);

    protected override async ValueTask<CatalogRecipeLookup<AIProfileTemplate>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var source = GetSource(token);

        if (string.IsNullOrEmpty(source))
        {
            context.Errors.Add(S["Could not find template source. The template will not be imported."]);

            return CatalogRecipeLookup<AIProfileTemplate>.Skip;
        }

        var name = GetName(token);

        if (string.IsNullOrEmpty(name))
        {
            context.Errors.Add(S["Could not find template name. The template will not be imported."]);

            return CatalogRecipeLookup<AIProfileTemplate>.Skip;
        }

        return CatalogRecipeLookup<AIProfileTemplate>.Found(await _templateManager.GetAsync(name, source));
    }

    protected override ValueTask UpdateAsync(AIProfileTemplate entry, JsonObject token)
        => _templateManager.UpdateAsync(entry, token);

    protected override async ValueTask<AIProfileTemplate> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        var source = GetSource(token);

        if (!_aiOptions.TemplateSources.TryGetValue(source, out _))
        {
            context.Errors.Add(S["Unable to find a template-source that can handle the source '{0}'.", source]);

            return null;
        }

        return await _templateManager.NewAsync(GetName(token), source, token);
    }

    protected override ValueTask<ValidationResultDetails> ValidateAsync(AIProfileTemplate entry)
        => _templateManager.ValidateAsync(entry);

    protected override ValueTask CreateAsync(AIProfileTemplate entry)
        => _templateManager.CreateAsync(entry);

    private static string GetSource(JsonObject token)
        => token[nameof(AIProfileTemplate.Source)]?.GetValue<string>()?.Trim();

    private static string GetName(JsonObject token)
        => token[nameof(AIProfileTemplate.Name)]?.GetValue<string>()?.Trim();
}

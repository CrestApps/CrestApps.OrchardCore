using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Recipes;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.AI.Recipes;

/// <summary>
/// Imports AI profiles. A profile is matched by its item id, then by its name.
/// </summary>
internal sealed class AIProfileStep : CatalogRecipeStepBase<AIProfile>
{
    /// <summary>
    /// The recipe step key used to identify this handler.
    /// </summary>
    public const string StepKey = "AIProfile";

    private readonly IAIProfileManager _profileManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileStep"/> class.
    /// </summary>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public AIProfileStep(
        IAIProfileManager profileManager,
        IStringLocalizer<AIProfileStep> stringLocalizer)
        : base(StepKey, "Profiles")
    {
        _profileManager = profileManager;
        S = stringLocalizer;
    }

    protected override ValueTask<AIProfile> FindByIdAsync(string id)
        => _profileManager.FindByIdAsync(id);

    protected override async ValueTask<CatalogRecipeLookup<AIProfile>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var name = token[nameof(AIProfile.Name)]?.GetValue<string>()?.Trim();

        return CatalogRecipeLookup<AIProfile>.Found(string.IsNullOrEmpty(name) ? null : await _profileManager.FindByNameAsync(name));
    }

    protected override ValueTask UpdateAsync(AIProfile entry, JsonObject token)
        => _profileManager.UpdateAsync(entry, token);

    protected override ValueTask<AIProfile> NewAsync(JsonObject token, RecipeExecutionContext context)
        => _profileManager.NewAsync(token);

    protected override ValueTask<ValidationResultDetails> ValidateAsync(AIProfile entry)
        => _profileManager.ValidateAsync(entry);

    protected override ValueTask CreateAsync(AIProfile entry)
        => _profileManager.CreateAsync(entry);
}

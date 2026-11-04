using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.AI.Recipes;

/// <summary>
/// Imports AI personas. A persona is matched by its item id, then by its technical name, and updated; otherwise it is
/// created. Matching by name keeps the links of profiles and interactions working across tenants, where item ids
/// differ.
/// </summary>
internal sealed class AIPersonaStep : CatalogRecipeStepBase<AIPersona>
{
    public const string StepKey = "AIPersonas";

    private readonly IAIPersonaManager _personaManager;
    private readonly INamedCatalog<AIPersona> _personasCatalog;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIPersonaStep"/> class.
    /// </summary>
    /// <param name="personaManager">The persona manager.</param>
    /// <param name="personasCatalog">The catalog of saved personas.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIPersonaStep(
        IAIPersonaManager personaManager,
        INamedCatalog<AIPersona> personasCatalog,
        IStringLocalizer<AIPersonaStep> stringLocalizer)
        : base(StepKey, "Personas")
    {
        _personaManager = personaManager;
        _personasCatalog = personasCatalog;
        S = stringLocalizer;
    }

    protected override ValueTask<AIPersona> FindByIdAsync(string id)
        => _personasCatalog.FindByIdAsync(id);

    protected override async ValueTask<CatalogRecipeLookup<AIPersona>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var name = GetName(token);

        return CatalogRecipeLookup<AIPersona>.Found(string.IsNullOrEmpty(name) ? null : await _personasCatalog.FindByNameAsync(name));
    }

    // The update keeps the saved name, because that is what links point to.
    protected override ValueTask UpdateAsync(AIPersona entry, JsonObject token)
        => _personaManager.UpdateAsync(entry, token);

    protected override async ValueTask<AIPersona> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        var name = GetName(token);

        if (string.IsNullOrEmpty(name))
        {
            context.Errors.Add(S["A persona needs a technical name. It was not imported."]);

            return null;
        }

        return await _personaManager.NewAsync(name, token);
    }

    protected override ValueTask<ValidationResultDetails> ValidateAsync(AIPersona entry)
        => _personaManager.ValidateAsync(entry);

    protected override ValueTask CreateAsync(AIPersona entry)
        => _personaManager.CreateAsync(entry);

    private static string GetName(JsonObject token)
        => token[nameof(AIPersona.Name)]?.GetValue<string>()?.Trim();
}

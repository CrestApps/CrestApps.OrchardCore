using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

/// <summary>
/// Imports AI personas. A persona is matched by its item id, then by its technical name, and updated; otherwise it is
/// created. Matching by name keeps the links of profiles and interactions working across tenants, where item ids
/// differ.
/// </summary>
internal sealed class AIPersonaStep : NamedRecipeStepHandler
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
        : base(StepKey)
    {
        _personaManager = personaManager;
        _personasCatalog = personasCatalog;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<AIPersonaStepModel>();
        var tokens = model?.Personas?.OfType<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            var id = token[nameof(AIPersona.ItemId)]?.GetValue<string>();
            var name = token[nameof(AIPersona.Name)]?.GetValue<string>()?.Trim();

            var persona = string.IsNullOrEmpty(id)
                ? null
                : await _personasCatalog.FindByIdAsync(id);

            if (persona is null && !string.IsNullOrEmpty(name))
            {
                persona = await _personasCatalog.FindByNameAsync(name);
            }

            if (persona is not null)
            {
                // The update keeps the saved name, because that is what links point to.
                await _personaManager.UpdateAsync(persona, token);
                await AddErrorsAsync(context, persona);

                continue;
            }

            if (string.IsNullOrEmpty(name))
            {
                context.Errors.Add(S["A persona needs a technical name. It was not imported."]);

                continue;
            }

            persona = await _personaManager.NewAsync(name, token);

            if (!string.IsNullOrEmpty(id) && UniqueId.IsValid(id))
            {
                persona.ItemId = id;
            }

            if (await AddErrorsAsync(context, persona))
            {
                continue;
            }

            await _personaManager.CreateAsync(persona);
        }
    }

    private async Task<bool> AddErrorsAsync(RecipeExecutionContext context, AIPersona persona)
    {
        var result = await _personaManager.ValidateAsync(persona);

        foreach (var error in result.Errors)
        {
            context.Errors.Add(error.ErrorMessage);
        }

        return !result.Succeeded;
    }

    private sealed class AIPersonaStepModel
    {
        public JsonArray Personas { get; set; }
    }
}

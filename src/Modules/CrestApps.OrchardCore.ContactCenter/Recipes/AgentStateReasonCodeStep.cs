using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.ContactCenter.Recipes;

/// <summary>
/// Imports Contact Center agent state reason codes carried by a recipe step, creating entries that do not exist and updating those that
/// do.
/// </summary>
internal sealed class AgentStateReasonCodeStep : NamedRecipeStepHandler
{
    private readonly IAgentStateReasonCodeManager _manager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateReasonCodeStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the agent state reason codes.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public AgentStateReasonCodeStep(
        IAgentStateReasonCodeManager manager,
        IStringLocalizer<AgentStateReasonCodeStep> stringLocalizer)
        : base(ContactCenterDeploymentSteps.AgentStateReasonCode)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<AgentStateReasonCodeStepModel>();
        var tokens = model.ReasonCodes?.OfType<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            AgentStateReasonCode entry = null;
            var isNew = false;

            var id = token[nameof(AgentStateReasonCode.ItemId)]?.GetValue<string>();
            var hasId = !string.IsNullOrEmpty(id);

            if (hasId)
            {
                entry = await _manager.FindByIdAsync(id);
            }

            if (entry is not null)
            {
                await _manager.UpdateAsync(entry, token);
            }
            else
            {
                isNew = true;
                entry = await _manager.NewAsync(token);

                if (hasId && UniqueId.IsValid(id))
                {
                    entry.ItemId = id;
                }
            }

            var validationResult = await _manager.ValidateAsync(entry);

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
                await _manager.CreateAsync(entry);
            }
        }
    }

    private sealed class AgentStateReasonCodeStepModel
    {
        public JsonArray ReasonCodes { get; set; }
    }
}

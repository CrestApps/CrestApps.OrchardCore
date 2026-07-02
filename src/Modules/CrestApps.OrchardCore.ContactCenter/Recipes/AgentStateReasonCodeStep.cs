using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.ContactCenter.Recipes;

internal sealed class AgentStateReasonCodeStep : NamedRecipeStepHandler
{
    public const string StepKey = "AgentStateReasonCode";

    private readonly IAgentStateReasonCodeManager _manager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateReasonCodeStep"/> class.
    /// </summary>
    /// <param name="manager">The reason code manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgentStateReasonCodeStep(
        IAgentStateReasonCodeManager manager,
        IStringLocalizer<AgentStateReasonCodeStep> stringLocalizer)
        : base(StepKey)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<AgentStateReasonCodesStepModel>();
        var tokens = model.ReasonCodes?.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            AgentStateReasonCode reasonCode = null;
            var isNew = false;

            var id = token[nameof(AgentStateReasonCode.ItemId)]?.GetValue<string>();
            var hasId = !string.IsNullOrEmpty(id);

            if (hasId)
            {
                reasonCode = await _manager.FindByIdAsync(id);
            }

            if (reasonCode is null)
            {
                var name = token[nameof(AgentStateReasonCode.Name)]?.GetValue<string>()?.Trim();

                if (!string.IsNullOrEmpty(name))
                {
                    reasonCode = await _manager.FindByNameAsync(name);
                }
            }

            if (reasonCode is not null)
            {
                await _manager.UpdateAsync(reasonCode, token);
            }
            else
            {
                isNew = true;
                reasonCode = await _manager.NewAsync(token);

                if (hasId && UniqueId.IsValid(id))
                {
                    reasonCode.ItemId = id;
                }
            }

            var validationResult = await _manager.ValidateAsync(reasonCode);

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
                await _manager.CreateAsync(reasonCode);
            }
        }
    }

    private sealed class AgentStateReasonCodesStepModel
    {
        /// <summary>
        /// Gets or sets the collection of reason codes to import.
        /// </summary>
        public JsonArray ReasonCodes { get; set; }
    }
}

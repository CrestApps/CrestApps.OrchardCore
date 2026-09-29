using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Telephony.Deployments;
using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Telephony.Recipes;

/// <summary>
/// Imports internal extensions carried by a recipe step, creating entries that do not exist and updating those that do.
/// An entry is matched by its identifier alone, and a created entry keeps the identifier it was exported with. The user
/// an extension rings is resolved by user name by the entry's handler, because user identifiers differ between
/// environments.
/// </summary>
internal sealed class TelephonyExtensionStep : NamedRecipeStepHandler
{
    private readonly ITelephonyExtensionManager _manager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyExtensionStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the internal extensions.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public TelephonyExtensionStep(
        ITelephonyExtensionManager manager,
        IStringLocalizer<TelephonyExtensionStep> stringLocalizer)
        : base(TelephonyDeploymentSteps.Extension)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<TelephonyExtensionStepModel>();
        var tokens = model.Extensions?.OfType<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            TelephonyExtension entry = null;
            var isNew = false;

            var id = token[nameof(TelephonyExtension.ItemId)]?.GetValue<string>();
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

    private sealed class TelephonyExtensionStepModel
    {
        public JsonArray Extensions { get; set; }
    }
}

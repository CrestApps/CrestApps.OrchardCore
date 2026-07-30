using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Recipes;

/// <summary>
/// Imports Omnichannel channel endpoints carried by a recipe step, creating entries that do not exist and updating
/// those that do.
/// </summary>
internal sealed class OmnichannelChannelEndpointStep : NamedRecipeStepHandler
{
    private readonly IOmnichannelChannelEndpointManager _manager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelChannelEndpointStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the channel endpoints.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public OmnichannelChannelEndpointStep(
        IOmnichannelChannelEndpointManager manager,
        IStringLocalizer<OmnichannelChannelEndpointStep> stringLocalizer)
        : base(OmnichannelDeploymentSteps.ChannelEndpoint)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<OmnichannelChannelEndpointStepModel>();
        var tokens = model.ChannelEndpoints?.OfType<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            OmnichannelChannelEndpoint entry = null;
            var isNew = false;

            var id = token[nameof(OmnichannelChannelEndpoint.ItemId)]?.GetValue<string>();
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

    private sealed class OmnichannelChannelEndpointStepModel
    {
        public JsonArray ChannelEndpoints { get; set; }
    }
}

using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Recipes;

/// <summary>
/// Imports Omnichannel campaign groups carried by a recipe step, creating entries that do not exist and updating those
/// that do.
/// </summary>
internal sealed class OmnichannelCampaignGroupStep : NamedRecipeStepHandler
{
    private readonly ICatalogManager<OmnichannelCampaignGroup> _manager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignGroupStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the campaign groups.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public OmnichannelCampaignGroupStep(
        ICatalogManager<OmnichannelCampaignGroup> manager,
        IStringLocalizer<OmnichannelCampaignGroupStep> stringLocalizer)
        : base(OmnichannelDeploymentSteps.CampaignGroup)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<OmnichannelCampaignGroupStepModel>();
        var tokens = model.CampaignGroups?.OfType<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            OmnichannelCampaignGroup entry = null;
            var isNew = false;

            var id = token[nameof(OmnichannelCampaignGroup.ItemId)]?.GetValue<string>();
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

    private sealed class OmnichannelCampaignGroupStepModel
    {
        public JsonArray CampaignGroups { get; set; }
    }
}

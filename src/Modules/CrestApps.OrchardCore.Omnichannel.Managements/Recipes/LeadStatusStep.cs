using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Recipes;

/// <summary>
/// Imports the lead statuses carried by a recipe step, creating entries that do not exist and updating those that do.
/// An entry is matched by its identifier, and then by its name, so replaying a plan never duplicates a seeded entry.
/// </summary>
internal sealed class LeadStatusStep : NamedRecipeStepHandler
{
    private readonly INamedCatalogManager<LeadStatus> _manager;
    private readonly INamedCatalog<LeadStatus> _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadStatusStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the lead statuses.</param>
    /// <param name="catalog">The catalog used to match entries by name.</param>
    public LeadStatusStep(
        INamedCatalogManager<LeadStatus> manager,
        INamedCatalog<LeadStatus> catalog)
        : base(OmnichannelDeploymentSteps.LeadStatus)
    {
        _manager = manager;
        _catalog = catalog;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<StepModel>();
        var tokens = model.LeadStatuses?.OfType<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            LeadStatus entry = null;
            var isNew = false;

            var id = token[nameof(LeadStatus.ItemId)]?.GetValue<string>();
            var hasId = !string.IsNullOrEmpty(id);

            if (hasId)
            {
                entry = await _manager.FindByIdAsync(id);
            }

            var name = token[nameof(LeadStatus.Name)]?.GetValue<string>()?.Trim();

            if (entry is null && !string.IsNullOrEmpty(name))
            {
                entry = await _catalog.FindByNameAsync(name);
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

    private sealed class StepModel
    {
        public JsonArray LeadStatuses { get; set; }
    }
}

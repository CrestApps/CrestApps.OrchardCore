using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Deployments;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Taxation.Recipes;

/// <summary>
/// Imports tax tables carried by a recipe step, creating entries that do not exist and updating those that do.
/// </summary>
internal sealed class TaxTableStep : NamedRecipeStepHandler
{
    private readonly INamedCatalogManager<TaxTable> _manager;

    internal readonly IStringLocalizer S;

    public TaxTableStep(
        INamedCatalogManager<TaxTable> manager,
        IStringLocalizer<TaxTableStep> stringLocalizer)
        : base(TaxationDeploymentSteps.TaxTable)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<TaxTableStepModel>();
        var tokens = model.TaxTables?.OfType<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            TaxTable entry = null;

            var id = token[nameof(TaxTable.ItemId)]?.GetValue<string>();
            var hasId = !string.IsNullOrEmpty(id);

            if (hasId)
            {
                entry = await _manager.FindByIdAsync(id);
            }

            if (entry is not null)
            {
                // Validate the resulting state on a detached copy before persisting, so an invalid imported
                // table is reported as a recipe error instead of being written to the store.
                var candidate = entry.Clone();
                TaxationDeploymentSerializer.Populate(candidate, token);

                var updateValidation = await _manager.ValidateAsync(candidate);

                if (!updateValidation.Succeeded)
                {
                    foreach (var error in updateValidation.Errors)
                    {
                        context.Errors.Add(error.ErrorMessage);
                    }

                    continue;
                }

                await _manager.UpdateAsync(entry, token);

                continue;
            }

            entry = await _manager.NewAsync(token);

            if (hasId && UniqueId.IsValid(id))
            {
                entry.ItemId = id;
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

            await _manager.CreateAsync(entry);
        }
    }

    private sealed class TaxTableStepModel
    {
        public JsonArray TaxTables { get; set; }
    }
}

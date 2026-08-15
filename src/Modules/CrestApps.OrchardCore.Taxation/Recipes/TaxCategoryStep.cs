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
/// Imports tax categories carried by a recipe step, creating entries that do not exist and updating those that do.
/// </summary>
internal sealed class TaxCategoryStep : NamedRecipeStepHandler
{
    private readonly INamedCatalogManager<TaxCategory> _manager;

    internal readonly IStringLocalizer S;

    public TaxCategoryStep(
        INamedCatalogManager<TaxCategory> manager,
        IStringLocalizer<TaxCategoryStep> stringLocalizer)
        : base(TaxationDeploymentSteps.TaxCategory)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<TaxCategoryStepModel>();
        var tokens = model.TaxCategories?.OfType<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            TaxCategory entry = null;
            var isNew = false;

            var id = token[nameof(TaxCategory.ItemId)]?.GetValue<string>();
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

    private sealed class TaxCategoryStepModel
    {
        public JsonArray TaxCategories { get; set; }
    }
}

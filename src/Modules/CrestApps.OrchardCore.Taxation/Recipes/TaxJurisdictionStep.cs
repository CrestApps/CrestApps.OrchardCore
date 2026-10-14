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
/// Imports tax jurisdictions carried by a recipe step, creating entries that do not exist and updating those that do.
/// </summary>
internal sealed class TaxJurisdictionStep : NamedRecipeStepHandler
{
    private readonly INamedCatalogManager<TaxJurisdiction> _manager;

    internal readonly IStringLocalizer S;

    public TaxJurisdictionStep(
        INamedCatalogManager<TaxJurisdiction> manager,
        IStringLocalizer<TaxJurisdictionStep> stringLocalizer)
        : base(TaxationDeploymentSteps.TaxJurisdiction)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<TaxJurisdictionStepModel>();
        var tokens = model.TaxJurisdictions?.OfType<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            TaxJurisdiction entry = null;
            var isNew = false;

            var id = token[nameof(TaxJurisdiction.ItemId)]?.GetValue<string>();
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

    private sealed class TaxJurisdictionStepModel
    {
        public JsonArray TaxJurisdictions { get; set; }
    }
}

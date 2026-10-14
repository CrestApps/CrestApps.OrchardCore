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
/// Imports tax rules carried by a recipe step, creating entries that do not exist and updating those that do.
/// </summary>
internal sealed class TaxRuleStep : NamedRecipeStepHandler
{
    private readonly INamedCatalogManager<TaxRule> _manager;

    internal readonly IStringLocalizer S;

    public TaxRuleStep(
        INamedCatalogManager<TaxRule> manager,
        IStringLocalizer<TaxRuleStep> stringLocalizer)
        : base(TaxationDeploymentSteps.TaxRule)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<TaxRuleStepModel>();
        var tokens = model.TaxRules?.OfType<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            TaxRule entry = null;
            var isNew = false;

            var id = token[nameof(TaxRule.ItemId)]?.GetValue<string>();
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

    private sealed class TaxRuleStepModel
    {
        public JsonArray TaxRules { get; set; }
    }
}

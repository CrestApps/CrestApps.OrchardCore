using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Edits what an AI deployment costs with its provider (<see cref="AIDeploymentPricing"/>), which the AI usage reports
/// use to estimate the cost of its usage.
/// </summary>
internal sealed class AIDeploymentPricingDisplayDriver : DisplayDriver<AIDeployment>
{
    internal readonly IStringLocalizer S;

    public AIDeploymentPricingDisplayDriver(IStringLocalizer<AIDeploymentPricingDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIDeployment deployment, BuildEditorContext context)
    {
        return Initialize<EditDeploymentPricingViewModel>("AIDeploymentPricing_Edit", model =>
        {
            if (deployment.GetPricing() is { } pricing)
            {
                model.Currency = pricing.Currency;
                model.Prices = pricing.Prices ?? [];
            }
        }).Location("Content:11");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDeployment deployment, UpdateEditorContext context)
    {
        var model = new EditDeploymentPricingViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        model.Prices ??= [];

        for (var index = 0; index < model.Prices.Count; index++)
        {
            var price = model.Prices[index];
            var meter = AIUsageMeters.Find(price.Meter);

            if (meter is null)
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{nameof(model.Prices)}[{index}].{nameof(price.Meter)}", S["Select what each price is for."]);
            }
            else if (meter.RequiresCountName && string.IsNullOrWhiteSpace(price.CountName))
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{nameof(model.Prices)}[{index}].{nameof(price.CountName)}", S["Enter the name of the count an 'Other reported count' price is for."]);
            }

            if (price.Amount < 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{nameof(model.Prices)}[{index}].{nameof(price.Amount)}", S["A price cannot be negative."]);
            }

            if (price.Increment < 0 || price.AboveInputTokens < 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{nameof(model.Prices)}[{index}].{nameof(price.Increment)}", S["The long-context threshold and billing increment cannot be negative."]);
            }
        }

        deployment.SetPricing(model.Currency, model.Prices);

        return Edit(deployment, context);
    }
}

using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class SubscriptionPartDisplayDriver : ContentPartDisplayDriver<SubscriptionPart>
{
    internal readonly IStringLocalizer S;

    public SubscriptionPartDisplayDriver(IStringLocalizer<SubscriptionPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(SubscriptionPart part, BuildPartDisplayContext context)
    {
        return Task.FromResult<IDisplayResult>(
            Combine(
                View(GetDisplayShapeType(context), part)
                .Location("Summary", "Content")
                .Location("Detail", "Content"),

                View("SubscriptionSignup", part)
                .Location("Summary", "Footer")
            )
        );
    }

    public override Task<IDisplayResult> EditAsync(SubscriptionPart part, BuildPartEditorContext context)
    {
        var shape = Initialize<SubscriptionPartViewModel>(GetEditorShapeType(context), viewModel =>
        {
            viewModel.InitialAmount = part.InitialAmount;
            viewModel.BillingAmount = part.BillingAmount;
            viewModel.BillingDuration = Math.Max(part.BillingDuration, 1);
            viewModel.DurationType = part.DurationType;
            viewModel.BillingCycleLimit = part.BillingCycleLimit;
            viewModel.SubscriptionDayDelay = part.SubscriptionDayDelay;
            viewModel.DurationTypes =
            [
                new SelectListItem(S["Year"], nameof(BillingDurationType.Year)),
                new SelectListItem(S["Month"], nameof(BillingDurationType.Month)),
                new SelectListItem(S["Week"], nameof(BillingDurationType.Week)),
                new SelectListItem(S["Day"], nameof(BillingDurationType.Day)),
            ];
        });

        return Task.FromResult<IDisplayResult>(shape);
    }

    public override async Task<IDisplayResult> UpdateAsync(SubscriptionPart part, UpdatePartEditorContext context)
    {
        var viewModel = new SubscriptionPartViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        if (viewModel.InitialAmount.HasValue && viewModel.InitialAmount.Value < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.InitialAmount), S["Initial Amount cannot be negative."]);
        }

        if (!viewModel.BillingAmount.HasValue)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.BillingAmount), S["Billing Amount is required."]);
        }
        else if (viewModel.BillingAmount.Value < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.BillingAmount), S["Billing Amount cannot be negative."]);
        }

        if (viewModel.BillingDuration < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.BillingDuration), S["Billing Duration cannot be less than one."]);
        }

        if (viewModel.BillingCycleLimit.HasValue && viewModel.BillingCycleLimit.Value < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.BillingCycleLimit), S["Billing Cycle Limit cannot be negative."]);
        }

        if (viewModel.SubscriptionDayDelay.HasValue && viewModel.SubscriptionDayDelay.Value < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.SubscriptionDayDelay), S["Subscription Day Delay cannot be negative."]);
        }

        part.InitialAmount = viewModel.InitialAmount;
        part.BillingAmount = viewModel.BillingAmount ?? 0;
        part.BillingDuration = viewModel.BillingDuration;
        part.DurationType = viewModel.DurationType;
        part.BillingCycleLimit = viewModel.BillingCycleLimit;
        part.SubscriptionDayDelay = viewModel.SubscriptionDayDelay;

        return await EditAsync(part, context);
    }
}

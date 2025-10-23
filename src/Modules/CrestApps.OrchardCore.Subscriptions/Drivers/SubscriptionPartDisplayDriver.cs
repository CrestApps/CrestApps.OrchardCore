using CrestApps.OrchardCore.Payments.Core.Models;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
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
        return CombineAsync(
            Initialize<DisplaySubscriptionViewModel>(GetDisplayShapeType(context), model =>
            {
                var price = part.ContentItem.As<ProductPart>();

                model.Price = price?.Price ?? 0;
                model.DurationType = part.DurationType;
                model.BillingDuration = part.BillingDuration;
                model.SubscriptionDayDelay = part.SubscriptionDayDelay;
                model.InitialAmountDescription = part.InitialAmountDescription;
                model.InitialAmount = part.InitialAmount;
                model.BillingCycleLimit = part.BillingCycleLimit;
            }).Location("Summary", "Content")
            .Location("Detail", "Content"),

            View("SubscriptionSignup", part)
            .Location("Summary", "Footer")
        );
    }

    public override IDisplayResult Edit(SubscriptionPart part, BuildPartEditorContext context)
    {
        return Initialize<SubscriptionPartViewModel>(GetEditorShapeType(context), model =>
        {
            model.InitialAmount = part.InitialAmount;
            model.InitialAmountDescription = part.InitialAmountDescription;
            model.BillingDuration = Math.Max(part.BillingDuration, 1);
            model.DurationType = part.DurationType;
            model.BillingCycleLimit = part.BillingCycleLimit;
            model.SubscriptionDayDelay = part.SubscriptionDayDelay;
            model.DurationTypes =
            [
                new SelectListItem(S["Year"], nameof(DurationType.Year)),
                new SelectListItem(S["Month"], nameof(DurationType.Month)),
                new SelectListItem(S["Week"], nameof(DurationType.Week)),
                new SelectListItem(S["Day"], nameof(DurationType.Day)),
            ];
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(SubscriptionPart part, UpdatePartEditorContext context)
    {
        var model = new SubscriptionPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.InitialAmount.HasValue && model.InitialAmount.Value < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.InitialAmount), S["Initial Amount cannot be negative."]);
        }

        if (model.InitialAmount > 0 && string.IsNullOrWhiteSpace(model.InitialAmountDescription))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.InitialAmount), S["Initial Amount Description is required."]);
        }

        if (model.BillingDuration < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.BillingDuration), S["Billing Duration cannot be less than one."]);
        }

        if (model.BillingCycleLimit.HasValue && model.BillingCycleLimit.Value < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.BillingCycleLimit), S["Billing Cycle Limit cannot be negative."]);
        }

        if (model.SubscriptionDayDelay.HasValue && model.SubscriptionDayDelay.Value < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SubscriptionDayDelay), S["Subscription Day Delay cannot be negative."]);
        }

        part.InitialAmountDescription = model.InitialAmountDescription;
        part.InitialAmount = model.InitialAmount;
        part.BillingDuration = model.BillingDuration;
        part.DurationType = model.DurationType;
        part.BillingCycleLimit = model.BillingCycleLimit;
        part.SubscriptionDayDelay = model.SubscriptionDayDelay;

        return Edit(part, context);
    }
}

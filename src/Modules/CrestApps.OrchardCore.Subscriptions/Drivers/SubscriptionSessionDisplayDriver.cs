using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class SubscriptionSessionDisplayDriver : DisplayDriver<SubscriptionSession>
{
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly IContentManager _contentManager;

    internal readonly IStringLocalizer S;

    public SubscriptionSessionDisplayDriver(
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IContentManager contentManager,
        IStringLocalizer<SubscriptionSessionDisplayDriver> stringLocalizer)
    {
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        _contentManager = contentManager;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> DisplayAsync(SubscriptionSession subscription, BuildDisplayContext context)
    {
        var summary = await BuildSummaryAsync(subscription);

        return Combine(
            View("SubscriptionsTitle_SummaryAdmin", summary)
                .Location("SummaryAdmin", "Header:1"),
            View("SubscriptionsTags_SummaryAdmin", summary)
                .Location("SummaryAdmin", "Tags:1"),
            Shape("SubscriptionsMeta_SummaryAdmin", new SubscriptionViewModel(subscription))
                .Location("SummaryAdmin", "Meta:20"),
            Shape("SubscriptionsActions_SummaryAdmin", new SubscriptionViewModel(subscription))
                .Location("SummaryAdmin", "Actions:5"),
            Shape("SubscriptionsButtonActions_SummaryAdmin", new SubscriptionViewModel(subscription))
                .Location("SummaryAdmin", "ActionsMenu:10")
        );
    }

    private async Task<SubscriptionSummaryAdminViewModel> BuildSummaryAsync(SubscriptionSession subscription)
    {
        var model = new SubscriptionSummaryAdminViewModel
        {
            SessionId = subscription.SessionId,
            Status = subscription.Status,
        };

        if (!string.IsNullOrEmpty(subscription.OwnerId))
        {
            var user = await _userManager.FindByIdAsync(subscription.OwnerId);

            if (user != null)
            {
                model.CustomerName = await _displayNameProvider.GetAsync(user);

                if (string.IsNullOrEmpty(model.CustomerName))
                {
                    model.CustomerName = user.UserName;
                }

                model.CustomerEmail = (user as User)?.Email;
            }
        }

        if (string.IsNullOrEmpty(model.CustomerName))
        {
            model.CustomerName = S["Guest"];
        }

        if (!string.IsNullOrEmpty(subscription.ContentItemVersionId))
        {
            var contentItem = await _contentManager.GetVersionAsync(subscription.ContentItemVersionId);

            model.PlanTitle = contentItem?.DisplayText;
        }

        if (string.IsNullOrEmpty(model.PlanTitle))
        {
            model.PlanTitle = subscription.ContentType;
        }

        if (subscription.TryGet<Invoice>(out var invoice))
        {
            model.Currency = invoice.Currency;
            model.DueNow = invoice.DueNow;
            model.InitialAmount = invoice.InitialPaymentAmount;

            var recurring = invoice.LineItems?.FirstOrDefault(x => x.Subscription != null);

            if (recurring != null)
            {
                model.RecurringAmount = recurring.UnitPrice * recurring.Quantity;
                model.BillingDuration = recurring.Subscription.BillingDuration;
                model.DurationType = recurring.Subscription.DurationType;
            }
        }

        return model;
    }

    public override IDisplayResult Edit(SubscriptionSession subscription, BuildEditorContext context)
    {
        return Initialize<SubscriptionsMetadata>("SubscriptionsMetadata_Edit", model =>
        {
            var metadata = subscription.GetOrCreate<SubscriptionsMetadata>();

            model.Subscriptions = metadata.Subscriptions;

        }).Location("Content:5");
    }
}

using CrestApps.OrchardCore.AI.Core.Webhooks;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Displays a webhook subscription in the admin list and edits it.
/// </summary>
public sealed class AIWebhookSubscriptionDisplayDriver : DisplayDriver<AIWebhookSubscription>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIWebhookSubscriptionDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">Encrypts the signing secret.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIWebhookSubscriptionDisplayDriver(IDataProtectionProvider dataProtectionProvider, IStringLocalizer<AIWebhookSubscriptionDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(AIWebhookSubscription subscription, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("AIWebhookSubscription_Fields_SummaryAdmin", subscription)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AIWebhookSubscription_Description_SummaryAdmin", subscription)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("AIWebhookSubscription_DefaultTags_SummaryAdmin", subscription)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Events:5"),
            View("AIWebhookSubscription_Buttons_SummaryAdmin", subscription).Location("Actions:5"),
            View("AIWebhookSubscription_ActionsMenu_SummaryAdmin", subscription).Location("ActionsMenu:10"));
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AIWebhookSubscription subscription, BuildEditorContext context)
    {
        return Initialize<AIWebhookSubscriptionViewModel>("AIWebhookSubscriptionFields_Edit", model =>
        {
            model.Name = subscription.Name;
            model.Url = subscription.Url;
            model.Enabled = subscription.Enabled;
            model.Events = subscription.Events?.ToArray() ?? [];
            model.HasSecret = !string.IsNullOrEmpty(subscription.ProtectedSecret);
        }).Location("Content:1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIWebhookSubscription subscription, UpdateEditorContext context)
    {
        var model = new AIWebhookSubscriptionViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["The name is required."]);
        }

        if (!Uri.TryCreate(model.Url?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Url), S["Enter the HTTPS address the events are posted to."]);
        }

        var events = (model.Events ?? []).Where(AIWebhookEvents.All.Contains).Distinct(StringComparer.Ordinal).ToList();

        if (events.Count == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Events), S["Choose at least one event."]);
        }

        if (string.IsNullOrEmpty(subscription.ProtectedSecret) && string.IsNullOrWhiteSpace(model.Secret))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Secret), S["Enter the secret the endpoint checks signatures with."]);
        }

        subscription.Name = model.Name?.Trim();
        subscription.Url = model.Url?.Trim();
        subscription.Enabled = model.Enabled;
        subscription.Events = events;

        // An empty secret keeps the one saved: it is never shown again.
        if (!string.IsNullOrWhiteSpace(model.Secret))
        {
            subscription.ProtectedSecret = _dataProtectionProvider.CreateProtector(AIWebhookPublisher.ProtectorPurpose).Protect(model.Secret.Trim());
        }

        return Edit(subscription, context);
    }
}

/// <summary>
/// Displays a webhook delivery in the delivery log.
/// </summary>
public sealed class AIWebhookDeliveryDisplayDriver : DisplayDriver<AIWebhookDelivery>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(AIWebhookDelivery delivery, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("AIWebhookDelivery_Fields_SummaryAdmin", delivery)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AIWebhookDelivery_DefaultTags_SummaryAdmin", delivery)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("AIWebhookDelivery_Description_SummaryAdmin", delivery)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("AIWebhookDelivery_Buttons_SummaryAdmin", delivery).Location("Actions:5"));
    }
}

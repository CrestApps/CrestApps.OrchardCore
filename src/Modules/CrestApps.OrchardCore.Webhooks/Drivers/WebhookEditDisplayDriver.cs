using CrestApps.OrchardCore.Webhooks.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Webhooks.Drivers;

/// <summary>
/// Builds the webhook editor in cards: the general settings, the events it sends and its signing secret.
/// </summary>
internal sealed class WebhookEditDisplayDriver : DisplayDriver<WebhookEditViewModel>
{
    internal readonly IStringLocalizer S;

    public WebhookEditDisplayDriver(IStringLocalizer<WebhookEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(WebhookEditViewModel model, BuildEditorContext context)
    {
        var results = new List<IDisplayResult>
        {
            Initialize<WebhookEditViewModel>("WebhookGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["General"], "10")),
            Initialize<WebhookEditViewModel>("WebhookEvents_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Events"], "20")),
        };

        if (!model.IsNew)
        {
            results.Add(Initialize<WebhookEditViewModel>("WebhookSecret_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Signing Secret"], "30")));
        }

        return Combine([.. results]);
    }

    public override async Task<IDisplayResult> UpdateAsync(WebhookEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.Url, m => m.IsEnabled, m => m.AllEvents, m => m.Events);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates.
    protected override void BuildPrefix(WebhookEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(WebhookEditViewModel source, WebhookEditViewModel model)
    {
        model.ItemId = source.ItemId;
        model.Name = source.Name;
        model.Url = source.Url;
        model.IsEnabled = source.IsEnabled;
        model.AllEvents = source.AllEvents;
        model.Events = source.Events;
        model.AvailableEvents = source.AvailableEvents;
        model.Secret = source.Secret;
    }
}
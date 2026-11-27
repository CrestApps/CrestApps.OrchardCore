using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Drivers;

/// <summary>
/// Builds the editor of the new broadcast page: one shape per card, so the fields are grouped by placement. The fields
/// keep the names the page posts, so the controller and the scripts of the page keep working.
/// </summary>
internal sealed class BroadcastCreateDisplayDriver : DisplayDriver<BroadcastCreateViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BroadcastCreateDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BroadcastCreateDisplayDriver(IStringLocalizer<BroadcastCreateDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(BroadcastCreateViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<BroadcastCreateViewModel>("BroadcastGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["General"], "10")),
            Initialize<BroadcastCreateViewModel>("BroadcastRecipients_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Recipients"], "20")),
            Initialize<BroadcastCreateViewModel>("BroadcastMessage_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Message"], "30")));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(BroadcastCreateViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Name,
            m => m.EndpointId,
            m => m.ContactAddresses,
            m => m.RecipientsText,
            m => m.Body);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    /// <inheritdoc/>
    protected override void BuildPrefix(BroadcastCreateViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(BroadcastCreateViewModel source, BroadcastCreateViewModel model)
    {
        model.Name = source.Name;
        model.EndpointId = source.EndpointId;
        model.Body = source.Body;
        model.RecipientsText = source.RecipientsText;
        model.ContactAddresses = source.ContactAddresses;
        model.Endpoints = source.Endpoints;
        model.EndpointChannels = source.EndpointChannels;
    }
}

using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// Builds the editor of a gift card: one shape per card.
/// </summary>
internal sealed class GiftCardEditDisplayDriver : DisplayDriver<GiftCardEditViewModel>
{
    internal readonly IStringLocalizer S;

    public GiftCardEditDisplayDriver(IStringLocalizer<GiftCardEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(GiftCardEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<GiftCardEditViewModel>("GiftCardEditRecipient_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Recipient"], "10")),
            Initialize<GiftCardEditViewModel>("GiftCardEditSettings_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Settings"], "20")));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(GiftCardEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.RecipientName, m => m.RecipientEmail, m => m.Expires, m => m.Note, m => m.IsEnabled);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    /// <inheritdoc/>
    protected override void BuildPrefix(GiftCardEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(GiftCardEditViewModel source, GiftCardEditViewModel target)
    {
        target.ItemId = source.ItemId;
        target.Code = source.Code;
        target.InitialValue = source.InitialValue;
        target.Balance = source.Balance;
        target.Currency = source.Currency;
        target.OrderId = source.OrderId;
        target.OrderNumber = source.OrderNumber;
        target.CreatedUtc = source.CreatedUtc;
        target.Activity = source.Activity;
        target.IsEnabled = source.IsEnabled;
        target.Expires = source.Expires;
        target.RecipientName = source.RecipientName;
        target.RecipientEmail = source.RecipientEmail;
        target.Note = source.Note;
    }
}

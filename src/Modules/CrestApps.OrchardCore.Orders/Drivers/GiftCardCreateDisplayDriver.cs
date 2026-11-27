using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// Builds the editor of the page that issues a gift card: one shape per card.
/// </summary>
internal sealed class GiftCardCreateDisplayDriver : DisplayDriver<GiftCardCreateViewModel>
{
    internal readonly IStringLocalizer S;

    public GiftCardCreateDisplayDriver(IStringLocalizer<GiftCardCreateDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(GiftCardCreateViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<GiftCardCreateViewModel>("GiftCardCreateCard_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Gift Card"], "10")),
            Initialize<GiftCardCreateViewModel>("GiftCardCreateRecipient_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Recipient"], "20")));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(GiftCardCreateViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Value, m => m.Currency, m => m.Code, m => m.Expires, m => m.Note, m => m.RecipientName, m => m.RecipientEmail, m => m.Message, m => m.SendEmail);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    /// <inheritdoc/>
    protected override void BuildPrefix(GiftCardCreateViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(GiftCardCreateViewModel source, GiftCardCreateViewModel target)
    {
        target.Value = source.Value;
        target.Currency = source.Currency;
        target.Code = source.Code;
        target.Expires = source.Expires;
        target.RecipientName = source.RecipientName;
        target.RecipientEmail = source.RecipientEmail;
        target.Message = source.Message;
        target.Note = source.Note;
        target.SendEmail = source.SendEmail;
    }
}

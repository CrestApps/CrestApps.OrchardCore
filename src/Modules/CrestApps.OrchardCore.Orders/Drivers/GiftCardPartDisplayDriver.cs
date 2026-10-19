using CrestApps.OrchardCore.Orders.Core.Models;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// Tells whoever edits a gift card product what selling it does. The part has nothing to set.
/// </summary>
internal sealed class GiftCardPartDisplayDriver : ContentPartDisplayDriver<GiftCardPart>
{
    public override IDisplayResult Edit(GiftCardPart part, BuildPartEditorContext context)
        => View("GiftCardPart_Edit", part).Location("Content:5");
}

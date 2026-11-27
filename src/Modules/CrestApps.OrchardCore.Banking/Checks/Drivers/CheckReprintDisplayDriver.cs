using CrestApps.OrchardCore.Banking.Checks.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Banking.Checks.Drivers;

/// <summary>
/// The fields of the page that prints a printed check again: the layout and why. The page posts them under their own
/// names, which the action binds.
/// </summary>
internal sealed class CheckReprintDisplayDriver : DisplayDriver<CheckReprintViewModel>
{
    internal readonly IStringLocalizer S;

    public CheckReprintDisplayDriver(IStringLocalizer<CheckReprintDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CheckReprintViewModel source, BuildEditorContext context)
        => Initialize<CheckReprintViewModel>("CheckReprint_Edit", model =>
        {
            model.Check = source.Check;
            model.LayoutId = source.LayoutId;
            model.Reason = source.Reason;
            model.LayoutOptions = source.LayoutOptions;
        }).Location(l => l.Zone("Content", "1").Card(S["Print Again"], "10"));

    protected override void BuildPrefix(CheckReprintViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }
}

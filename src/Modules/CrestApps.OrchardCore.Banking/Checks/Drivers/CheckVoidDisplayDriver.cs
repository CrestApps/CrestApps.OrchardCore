using CrestApps.OrchardCore.Banking.Checks.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Banking.Checks.Drivers;

/// <summary>
/// The fields of the page that voids a check: the date and why. The page posts them under their own names, which the
/// action reads as parameters.
/// </summary>
internal sealed class CheckVoidDisplayDriver : DisplayDriver<CheckVoidViewModel>
{
    internal readonly IStringLocalizer S;

    public CheckVoidDisplayDriver(IStringLocalizer<CheckVoidDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CheckVoidViewModel source, BuildEditorContext context)
        => Initialize<CheckVoidViewModel>("CheckVoid_Edit", model =>
        {
            model.Check = source.Check;
            model.VoidedOn = source.VoidedOn;
            model.Reason = source.Reason;
        }).Location(l => l.Zone("Content", "1").Card(S["Void"], "10"));

    protected override void BuildPrefix(CheckVoidViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }
}

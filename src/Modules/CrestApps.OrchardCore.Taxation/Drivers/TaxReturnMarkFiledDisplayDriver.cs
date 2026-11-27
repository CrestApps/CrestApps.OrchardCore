using CrestApps.OrchardCore.Taxation.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Drivers;

/// <summary>
/// The fields of the form that marks a tax return as filed: the day and the confirmation number. The page posts them
/// under their own names, read by the action as parameters.
/// </summary>
internal sealed class TaxReturnMarkFiledDisplayDriver : DisplayDriver<TaxReturnMarkFiledViewModel>
{
    internal readonly IStringLocalizer S;

    public TaxReturnMarkFiledDisplayDriver(IStringLocalizer<TaxReturnMarkFiledDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TaxReturnMarkFiledViewModel source, BuildEditorContext context)
        => Initialize<TaxReturnMarkFiledViewModel>("TaxReturnMarkFiled_Edit", model => model.Today = source.Today)
            .Location(l => l.Zone("Content", "1").Card(S["Mark as Filed"], "10"));

    protected override void BuildPrefix(TaxReturnMarkFiledViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }
}

using CrestApps.OrchardCore.Banking.Disbursements.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Banking.Disbursements.Drivers;

/// <summary>
/// The fields of the page that imports the bank's return and change file. The file is posted under its own name, which
/// the action reads as a parameter.
/// </summary>
internal sealed class DisbursementReturnsDisplayDriver : DisplayDriver<DisbursementReturnsViewModel>
{
    internal readonly IStringLocalizer S;

    public DisbursementReturnsDisplayDriver(IStringLocalizer<DisbursementReturnsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(DisbursementReturnsViewModel source, BuildEditorContext context)
        => Initialize<DisbursementReturnsViewModel>("DisbursementReturns_Edit", model => model.Result = source.Result)
            .Location(l => l.Zone("Content", "1").Card(S["Return and Change File"], "10"));

    protected override void BuildPrefix(DisbursementReturnsViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }
}

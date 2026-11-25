using CrestApps.OrchardCore.Banking.Checks.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Banking.Checks.Drivers;

/// <summary>
/// The fields of the page that prints the queued checks of a bank account: the checks that print and the stock they
/// print on. The page posts the fields under their own names, which the action binds.
/// </summary>
internal sealed class CheckPrintRunDisplayDriver : DisplayDriver<CheckPrintRunViewModel>
{
    internal readonly IStringLocalizer S;

    public CheckPrintRunDisplayDriver(IStringLocalizer<CheckPrintRunDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CheckPrintRunViewModel source, BuildEditorContext context)
    {
        return Combine(
            Initialize<CheckPrintRunViewModel>("CheckPrintRunChecks_Edit", model => Populate(model, source))
                .Location(l => l.Zone("Content", "1").Card(S["Checks Waiting to Print"], "10")),
            Initialize<CheckPrintRunViewModel>("CheckPrintRunStock_Edit", model => Populate(model, source))
                .Location(l => l.Zone("Content", "2").Card(S["Check Stock"], "20")));
    }

    protected override void BuildPrefix(CheckPrintRunViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(CheckPrintRunViewModel model, CheckPrintRunViewModel source)
    {
        model.BankAccountId = source.BankAccountId;
        model.LayoutId = source.LayoutId;
        model.FirstNumber = source.FirstNumber;
        model.CheckIds = source.CheckIds;
        model.MisprintedCheckIds = source.MisprintedCheckIds;
        model.PrintRunId = source.PrintRunId;
        model.BankAccountName = source.BankAccountName;
        model.Checks = source.Checks;
        model.First = source.First;
        model.BankAccountOptions = source.BankAccountOptions;
        model.LayoutOptions = source.LayoutOptions;
    }
}

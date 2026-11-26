using CrestApps.OrchardCore.Banking.Checks.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Banking.Checks.Drivers;

/// <summary>
/// The page of the next check numbers: the number each bank account's next check gets, and the form that loads new
/// stock. The page posts the fields under their own names, which the action binds.
/// </summary>
internal sealed class CheckNextNumberDisplayDriver : DisplayDriver<CheckNextNumberViewModel>
{
    internal readonly IStringLocalizer S;

    public CheckNextNumberDisplayDriver(IStringLocalizer<CheckNextNumberDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CheckNextNumberViewModel source, BuildEditorContext context)
    {
        return Combine(
            Initialize<CheckNextNumberViewModel>("CheckNextNumberCurrent_Edit", model => Populate(model, source))
                .Location(l => l.Zone("Content", "1").Card(S["Now"], "10")),
            Initialize<CheckNextNumberViewModel>("CheckNextNumber_Edit", model => Populate(model, source))
                .Location(l => l.Zone("Content", "2").Card(S["Load New Stock"], "20")));
    }

    protected override void BuildPrefix(CheckNextNumberViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(CheckNextNumberViewModel model, CheckNextNumberViewModel source)
    {
        model.BankAccountId = source.BankAccountId;
        model.NextNumber = source.NextNumber;
        model.Current = source.Current;
        model.BankAccountOptions = source.BankAccountOptions;
    }
}

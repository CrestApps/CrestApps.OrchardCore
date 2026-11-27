using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Banking.Checks.Drivers;

/// <summary>
/// The Change card of the editor of a check that waits to print: why it changes. The reason is posted under its own
/// name, which the controller reads, so the driver binds nothing.
/// </summary>
internal sealed class CheckRecordChangeDisplayDriver : DisplayDriver<CheckRecord>
{
    internal readonly IStringLocalizer S;

    public CheckRecordChangeDisplayDriver(IStringLocalizer<CheckRecordChangeDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CheckRecord check, BuildEditorContext context)
    {
        // A new check has no change to explain.
        if (context.IsNew)
        {
            return null;
        }

        return Initialize<CheckEditViewModel>("CheckRecordChange_Edit", model => model.Check = check)
            .Location(l => l.Zone("Content", "3").Card(S["Change"], "30"));
    }

    protected override void BuildPrefix(CheckRecord model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }
}

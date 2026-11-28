using CrestApps.OrchardCore.Banking.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Banking.Drivers;

/// <summary>
/// The fields of the page where a person links their bank: what linking does, and, for a new account, who holds it. The
/// page posts the fields under their own names, which the link flow reads as parameters.
/// </summary>
internal sealed class BankAccountLinkDisplayDriver : DisplayDriver<BankAccountLinkViewModel>
{
    internal readonly IStringLocalizer S;

    public BankAccountLinkDisplayDriver(IStringLocalizer<BankAccountLinkDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(BankAccountLinkViewModel source, BuildEditorContext context)
    {
        return Combine(
            Initialize<BankAccountLinkViewModel>("BankAccountLinkProvider_Edit", model => Populate(model, source))
                .Location(l => l.Zone("Content", "1").Card(S["Link Your Bank With {0}", source.ProviderDisplayName], "10")),
            Initialize<BankAccountLinkViewModel>("BankAccountLinkAccount_Edit", model => Populate(model, source))
                .Location(l => l.Zone("Content", "2").Card(S["The Account"], "20"))
                .RenderWhen(() => Task.FromResult(source.IsNew)));
    }

    // The fields keep the names the page posts, which the link flow reads.
    protected override void BuildPrefix(BankAccountLinkViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(BankAccountLinkViewModel model, BankAccountLinkViewModel source)
    {
        model.ProviderName = source.ProviderName;
        model.ProviderDisplayName = source.ProviderDisplayName;
        model.IsNew = source.IsNew;
        model.AccountName = source.AccountName;
        model.MaskedNumber = source.MaskedNumber;
        model.Name = source.Name;
        model.HolderName = source.HolderName;
    }
}

using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Invoicing.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Invoicing.Navigation;

/// <summary>
/// Adds Invoices to the customer's account menu.
/// </summary>
public sealed class InvoicesAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public InvoicesAccountMenu(IStringLocalizer<InvoicesAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Invoices"], "39", invoices => invoices
            .Id("account-invoices")
            .AddClass("fa-solid fa-file-invoice")
            .Url("~/my-invoices")
            .Permission(InvoicingPermissions.ViewOwnInvoices));

        return ValueTask.CompletedTask;
    }
}

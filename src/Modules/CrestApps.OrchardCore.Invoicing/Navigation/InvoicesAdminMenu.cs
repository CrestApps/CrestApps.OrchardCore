using CrestApps.OrchardCore.Invoicing.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Invoicing.Navigation;

/// <summary>
/// Adds Commerce > Invoices, beside Transactions.
/// </summary>
public sealed class InvoicesAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", InvoicingConstants.Features.Area },
        { "controller", "InvoicesAdmin" },
        { "action", "Index" },
    };

    internal readonly IStringLocalizer S;

    public InvoicesAdminMenu(IStringLocalizer<InvoicesAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Invoices"], S["Invoices"].PrefixPosition("4"), invoices => invoices
                    .Id("invoices")
                    .Action("Index", "InvoicesAdmin", _routeValues)
                    .Permission(InvoicingPermissions.ViewInvoices)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}

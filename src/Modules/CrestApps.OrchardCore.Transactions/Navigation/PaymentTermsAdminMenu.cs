using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Transactions.Navigation;

/// <summary>
/// Adds <b>Commerce &gt; Payment Terms</b> to the admin menu.
/// </summary>
public sealed class PaymentTermsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", TransactionsConstants.Features.Area },
        { "controller", "PaymentTerms" },
        { "action", "Index" },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentTermsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PaymentTermsAdminMenu(IStringLocalizer<PaymentTermsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Payment Terms"], S["Payment Terms"].PrefixPosition("7"), terms => terms
                    .Id("paymentTerms")
                    .Action("Index", "PaymentTerms", _routeValues)
                    .Permission(TransactionsPermissions.ManagePaymentTerms)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}

using CrestApps.OrchardCore.Commerce;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Stripe.Navigation;

/// <summary>
/// Adds the buyer's saved cards to the storefront account.
/// </summary>
public sealed class StripeAccountMenu : INavigationProvider
{
    internal readonly IStringLocalizer S;

    public StripeAccountMenu(IStringLocalizer<StripeAccountMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!string.Equals(name, CommerceConstants.Account.MenuName, StringComparison.Ordinal))
        {
            return ValueTask.CompletedTask;
        }

        builder.Add(S["Payment methods"], "60", methods => methods
            .Id("account-payment-methods")
            .AddClass("fa-regular fa-credit-card")
            .Action("Index", "AccountPaymentMethods", new { area = "CrestApps.OrchardCore.Stripe" }));

        return ValueTask.CompletedTask;
    }
}

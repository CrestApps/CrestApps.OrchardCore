using CrestApps.OrchardCore.Commerce;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Stripe.Navigation;

/// <summary>
/// Adds the buyer's saved cards to the storefront account.
/// </summary>
public sealed class StripeAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public StripeAccountMenu(IStringLocalizer<StripeAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Payment methods"], "60", methods => methods
            .Id("account-payment-methods")
            .AddClass("fa-regular fa-credit-card")
            .Action("Index", "AccountPaymentMethods", new { area = "CrestApps.OrchardCore.Stripe" }));

        return ValueTask.CompletedTask;
    }
}

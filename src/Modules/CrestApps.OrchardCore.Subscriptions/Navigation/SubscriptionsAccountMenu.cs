using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Subscriptions.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Subscriptions.Navigation;

/// <summary>
/// Adds the customer's subscriptions to the storefront account.
/// </summary>
public sealed class SubscriptionsAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public SubscriptionsAccountMenu(IStringLocalizer<SubscriptionsAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Subscriptions"], "30", subscriptions => subscriptions
            .Id("account-subscriptions")
            .AddClass("fa-solid fa-rotate")
            .Action("Index", "AccountSubscriptions", new { area = "CrestApps.OrchardCore.Subscriptions" })
            .Permission(SubscriptionPermissions.ManageOwnSubscriptions));

        return ValueTask.CompletedTask;
    }
}

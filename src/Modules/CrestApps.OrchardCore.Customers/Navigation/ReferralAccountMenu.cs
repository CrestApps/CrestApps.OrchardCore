using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Customers.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Customers.Navigation;

/// <summary>
/// Adds Refer a friend to the storefront account menu.
/// </summary>
public sealed class ReferralAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public ReferralAccountMenu(IStringLocalizer<ReferralAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Refer a friend"], "70", refer => refer
                .Id("account-referrals")
                .AddClass("fa-solid fa-user-plus")
                .Action("Index", "ReferralAccount", new { area = "CrestApps.OrchardCore.Customers" })
                .Permission(CustomerPermissions.ManageOwnCustomerProfile));

        return ValueTask.CompletedTask;
    }
}

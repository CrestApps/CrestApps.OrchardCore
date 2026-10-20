using CrestApps.OrchardCore.Commerce;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Customers.Navigation;

/// <summary>
/// Adds Store credit to the storefront account menu.
/// </summary>
public sealed class StoreCreditAccountMenu : INavigationProvider
{
    internal readonly IStringLocalizer S;

    public StoreCreditAccountMenu(IStringLocalizer<StoreCreditAccountMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!string.Equals(name, CommerceConstants.Account.MenuName, StringComparison.Ordinal))
        {
            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Store credit"], "60", credit => credit
                .Id("account-store-credit")
                .AddClass("fa-solid fa-wallet")
                .Action("Index", "StoreCreditAccount", new { area = "CrestApps.OrchardCore.Customers" })
                .Permission(CustomerPermissions.ManageOwnCustomerProfile));

        return ValueTask.CompletedTask;
    }
}

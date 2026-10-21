using CrestApps.OrchardCore.Commerce;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Customers.Navigation;

/// <summary>
/// Adds Store credit to the storefront account menu.
/// </summary>
public sealed class StoreCreditAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public StoreCreditAccountMenu(IStringLocalizer<StoreCreditAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Store credit"], "60", credit => credit
                .Id("account-store-credit")
                .AddClass("fa-solid fa-wallet")
                .Action("Index", "StoreCreditAccount", new { area = "CrestApps.OrchardCore.Customers" })
                .Permission(CustomerPermissions.ManageOwnCustomerProfile));

        return ValueTask.CompletedTask;
    }
}

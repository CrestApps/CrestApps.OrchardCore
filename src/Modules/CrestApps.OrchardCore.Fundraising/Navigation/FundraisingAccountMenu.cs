using CrestApps.OrchardCore.Commerce;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Fundraising.Navigation;

/// <summary>
/// Adds the donor's donations and receipts to the storefront account.
/// </summary>
public sealed class FundraisingAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public FundraisingAccountMenu(IStringLocalizer<FundraisingAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Donations"], "24", donations => donations
            .Id("account-donations")
            .AddClass("fa-solid fa-hand-holding-heart")
            .Action("Index", "AccountDonations", new { area = FundraisingConstants.Feature }));

        return ValueTask.CompletedTask;
    }
}

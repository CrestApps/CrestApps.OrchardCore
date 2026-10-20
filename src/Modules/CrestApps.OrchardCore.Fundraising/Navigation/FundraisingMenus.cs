using CrestApps.OrchardCore.Commerce;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Fundraising.Navigation;

/// <summary>
/// Settings › Commerce › Fundraising.
/// </summary>
public sealed class FundraisingAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public FundraisingAdminMenu(IStringLocalizer<FundraisingAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Fundraising"], S["Fundraising"].PrefixPosition(), fundraising => fundraising
                        .AddClass("fundraising-settings")
                        .Id("fundraisingSettings")
                        .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = FundraisingConstants.SettingsGroupId })
                        .Permission(FundraisingPermissions.ManageFundraising)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Adds the donor's donations and receipts to the storefront account.
/// </summary>
public sealed class FundraisingAccountMenu : INavigationProvider
{
    internal readonly IStringLocalizer S;

    public FundraisingAccountMenu(IStringLocalizer<FundraisingAccountMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (string.Equals(name, CommerceConstants.Account.MenuName, StringComparison.Ordinal))
        {
            builder.Add(S["Donations"], "24", donations => donations
                .Id("account-donations")
                .AddClass("fa-solid fa-hand-holding-heart")
                .Action("Index", "AccountDonations", new { area = FundraisingConstants.Feature }));
        }

        return ValueTask.CompletedTask;
    }
}

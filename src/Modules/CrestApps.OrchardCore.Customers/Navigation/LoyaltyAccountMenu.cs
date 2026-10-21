using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Customers.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Customers.Navigation;

/// <summary>
/// Adds Rewards to the storefront account menu.
/// </summary>
public sealed class LoyaltyAccountMenu : NamedNavigationProvider
{
    private readonly Services.LoyaltyService _loyalty;

    internal readonly IStringLocalizer S;

    public LoyaltyAccountMenu(Services.LoyaltyService loyalty, IStringLocalizer<LoyaltyAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        _loyalty = loyalty;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var programName = (await _loyalty.GetSettingsAsync()).ProgramName;

        builder
            .Add(new LocalizedString(programName, programName), "65", rewards => rewards
                .Id("account-rewards")
                .AddClass("fa-solid fa-star")
                .Action("Index", "LoyaltyAccount", new { area = "CrestApps.OrchardCore.Customers" })
                .Permission(CustomerPermissions.ManageOwnCustomerProfile));
    }
}

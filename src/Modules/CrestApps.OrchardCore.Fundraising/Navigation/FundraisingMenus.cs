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
                        .Id("fundraisingSettings")
                        .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = FundraisingConstants.SettingsGroupId })
                        .Permission(FundraisingPermissions.ManageFundraising)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}

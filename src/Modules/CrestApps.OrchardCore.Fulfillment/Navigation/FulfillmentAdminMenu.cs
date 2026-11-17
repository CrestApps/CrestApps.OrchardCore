using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Fulfillment.Navigation;

/// <summary>
/// Settings › Commerce › Fulfillment partners.
/// </summary>
internal sealed class FulfillmentAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public FulfillmentAdminMenu(IStringLocalizer<FulfillmentAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Fulfillment partners"], S["Fulfillment partners"].PrefixPosition(), partners => partners
                        .Id("fulfillmentPartnersSettings")
                        .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = FulfillmentConstants.SettingsGroupId })
                        .Permission(FulfillmentPermissions.ManageFulfillmentPartners)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}

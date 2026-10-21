using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Shipping.Navigation;

internal sealed class ShippingAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public ShippingAdminMenu(IStringLocalizer<ShippingAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Shipping"], S["Shipping"].PrefixPosition("0.4"), shipping => shipping
                    .Id("shipping")
                    .Action("Index", "ShippingZones", ShippingConstants.Features.Shipping)
                    .Permission(ShippingPermissions.ManageShipping)
                    .LocalNav()
                )
            )
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Shipping"], S["Shipping"].PrefixPosition(), shipping => shipping
                        .Id("shippingSettings")
                        .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = ShippingConstants.SettingsGroupId })
                        .Permission(ShippingPermissions.ManageShipping)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}

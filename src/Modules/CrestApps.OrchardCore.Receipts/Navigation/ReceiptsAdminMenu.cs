using CrestApps.OrchardCore.Receipts.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Receipts.Navigation;

/// <summary>
/// Adds the receipt settings entry to the admin navigation.
/// </summary>
public sealed class ReceiptsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", ReceiptsConstants.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReceiptsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReceiptsAdminMenu(IStringLocalizer<ReceiptsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Receipts"], S["Receipts"].PrefixPosition(), receipts => receipts
                        .AddClass("receipts")
                        .Id("receipts")
                        .Action("Index", "Admin", _routeValues)
                        .Permission(ReceiptsPermissions.ManageReceiptSettings)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}

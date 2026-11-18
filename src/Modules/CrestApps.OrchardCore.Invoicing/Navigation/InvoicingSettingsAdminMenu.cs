using CrestApps.OrchardCore.Invoicing.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Invoicing.Navigation;

/// <summary>
/// Adds <b>Settings &gt; Commerce &gt; Invoicing</b>.
/// </summary>
public sealed class InvoicingSettingsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", InvoicingConstants.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvoicingSettingsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InvoicingSettingsAdminMenu(IStringLocalizer<InvoicingSettingsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Invoicing"], S["Invoicing"].PrefixPosition(), invoicing => invoicing
                        .Id("invoicingSettings")
                        .Action("Index", "Admin", _settingsRouteValues)
                        .Permission(InvoicingPermissions.ManageInvoicingSettings)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}

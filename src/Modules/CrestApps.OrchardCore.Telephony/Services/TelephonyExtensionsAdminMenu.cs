using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Adds the internal extensions entry to the Interaction Center admin navigation, alongside the other
/// interaction-management screens.
/// </summary>
public sealed class TelephonyExtensionsAdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyExtensionsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TelephonyExtensionsAdminMenu(IStringLocalizer<TelephonyExtensionsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Interaction Center"], "80", interactionCenter => interactionCenter
                .AddClass("interaction-center")
                .Id("interactionCenter")
                .Add(S["Management"], S["Management"].PrefixPosition(), management => management
                    .AddClass("interaction-center-management")
                    .Id("interactionCenterManagement")
                    .Add(S["Extensions"], S["Extensions"].PrefixPosition(), extensions => extensions
                        .AddClass("telephony-extensions")
                        .Id("telephonyExtensions")
                        .Action("Index", "Extensions", "CrestApps.OrchardCore.Telephony")
                        .Permission(TelephonyPermissions.ManageExtensions)
                        .LocalNav())),
                priority: 1);

        return ValueTask.CompletedTask;
    }
}

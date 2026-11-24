using CrestApps.OrchardCore.ContactCenter;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Adds the internal extensions screen to the admin navigation: in the Interaction Center, beside the other
/// interaction-management screens, while Contact Center (which owns that menu and its icon) is on; otherwise under
/// Settings &gt; Communication, beside the telephony settings.
/// </summary>
public sealed class TelephonyExtensionsAdminMenu : AdminNavigationProvider
{
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyExtensionsAdminMenu"/> class.
    /// </summary>
    /// <param name="shellFeaturesManager">The shell features manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TelephonyExtensionsAdminMenu(IShellFeaturesManager shellFeaturesManager, IStringLocalizer<TelephonyExtensionsAdminMenu> stringLocalizer)
    {
        _shellFeaturesManager = shellFeaturesManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        if (await _shellFeaturesManager.IsFeatureEnabledAsync(ContactCenterConstants.Feature.Area))
        {
            builder
                .Add(S["Interaction Center"], "80", interactionCenter => interactionCenter
                    .AddClass("interaction-center")
                    .Id("interactionCenter")
                    .Add(S["Management"], S["Management"].PrefixPosition(), management => management
                        .AddClass("interaction-center-management")
                        .Id("interactionCenterManagement")
                        .Add(S["Extensions"], S["Extensions"].PrefixPosition(), Extensions)),
                    priority: 1);

            return;
        }

        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Communication"], S["Communication"].PrefixPosition(), communication => communication
                    .Add(S["Extensions"], S["Extensions"].PrefixPosition(), Extensions)));
    }

    private static void Extensions(NavigationItemBuilder extensions)
        => extensions
            .AddClass("telephony-extensions")
            .Id("telephonyExtensions")
            .Action("Index", "Extensions", "CrestApps.OrchardCore.Telephony")
            .Permission(TelephonyPermissions.ManageExtensions)
            .LocalNav();
}

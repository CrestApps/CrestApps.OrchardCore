using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Adds the Telnyx numbers page beside the telephony settings.
/// </summary>
public sealed class TelnyxNumbersAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxNumbersAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TelnyxNumbersAdminMenu(IStringLocalizer<TelnyxNumbersAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Communication"], S["Communication"].PrefixPosition(), communication => communication
                    .Add(S["Telnyx numbers"], S["Telnyx numbers"].PrefixPosition(), numbers => numbers
                        .Id("telnyxNumbers")
                        .Action("Index", "TelnyxNumbers", TelnyxConstants.Feature.Area)
                        .Permission(TelephonyPermissions.ManageTelephonySettings)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}

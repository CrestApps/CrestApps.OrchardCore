using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds the Business hours management entry to the Interaction Center admin navigation. It is a separate provider from
/// <see cref="ContactCenterAdminMenu"/> so the entry travels with the Business Hours feature and appears even when the
/// Work Distribution feature (which owns the other entries) is not enabled — for example when only automated
/// Omnichannel conversations pulled Business Hours in. OrchardCore merges the shared parent nodes by name.
/// </summary>
public sealed class BusinessHoursAdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BusinessHoursAdminMenu(IStringLocalizer<BusinessHoursAdminMenu> stringLocalizer)
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
                    .Add(S["Business hours"], S["Business hours"].PrefixPosition(), businessHours => businessHours
                        .AddClass("contact-center-business-hours")
                        .Id("contactCenterBusinessHours")
                        .Action("Index", "BusinessHoursCalendars", "CrestApps.OrchardCore.ContactCenter")
                        .Permission(ContactCenterPermissions.ManageBusinessHours)
                        .LocalNav())),
                priority: 1);

        return ValueTask.CompletedTask;
    }
}

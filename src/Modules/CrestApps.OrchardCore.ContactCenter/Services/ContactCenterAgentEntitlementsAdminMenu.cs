using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds manager-owned agent entitlement configuration to the Interaction Center admin navigation.
/// </summary>
public sealed class ContactCenterAgentEntitlementsAdminMenu : AdminNavigationProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAgentEntitlementsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterAgentEntitlementsAdminMenu(
        IStringLocalizer<ContactCenterAgentEntitlementsAdminMenu> stringLocalizer)
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
                .Add(S["Agent entitlements"], S["Agent entitlements"].PrefixPosition(), entitlements => entitlements
                    .AddClass("contact-center-agent-entitlements")
                    .Id("contactCenterAgentEntitlements")
                    .Action("Index", "AgentEntitlements", "CrestApps.OrchardCore.ContactCenter")
                    .Permission(ContactCenterPermissions.ManageAgents)
                    .LocalNav()
                ),
                priority: 1);

        return ValueTask.CompletedTask;
    }
}

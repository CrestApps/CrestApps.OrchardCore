using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Security;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Edits what a subscription plan entitles a subscriber to while their subscription is current.
/// </summary>
/// <remarks>
/// The roles are chosen per plan rather than site-wide because different tiers grant different things,
/// which a single site-wide list cannot express.
/// </remarks>
public sealed class SubscriptionEntitlementPartDisplayDriver : ContentPartDisplayDriver<SubscriptionEntitlementPart>
{
    private readonly RoleManager<IRole> _roleManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionEntitlementPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="roleManager">The role manager used to list the roles a plan can grant.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubscriptionEntitlementPartDisplayDriver(
        RoleManager<IRole> roleManager,
        IStringLocalizer<SubscriptionEntitlementPartDisplayDriver> stringLocalizer)
    {
        _roleManager = roleManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(SubscriptionEntitlementPart part, BuildPartEditorContext context)
        => Initialize<SubscriptionEntitlementPartViewModel>(GetEditorShapeType(context), model =>
        {
            var selected = new HashSet<string>(part.RoleNames ?? [], StringComparer.OrdinalIgnoreCase);

            model.Roles = [.. _roleManager.Roles
                .Select(role => role.RoleName)
                .Where(roleName => !string.IsNullOrEmpty(roleName))
                .OrderBy(roleName => roleName)
                .Select(roleName => new SubscriptionEntitlementRoleEntry
                {
                    RoleName = roleName,
                    IsSelected = selected.Contains(roleName),
                })];
        });

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(SubscriptionEntitlementPart part, UpdatePartEditorContext context)
    {
        var model = new SubscriptionEntitlementPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.RoleNames = [.. (model.Roles ?? [])
            .Where(role => role.IsSelected && !string.IsNullOrWhiteSpace(role.RoleName))
            .Select(role => role.RoleName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        return Edit(part, context);
    }
}

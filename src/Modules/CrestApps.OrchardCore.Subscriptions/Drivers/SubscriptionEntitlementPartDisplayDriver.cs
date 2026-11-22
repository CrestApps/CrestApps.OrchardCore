using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Security.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Edits what a subscription plan entitles a subscriber to while their subscription is current.
/// </summary>
/// <remarks>
/// The roles are chosen per plan rather than site-wide because different tiers grant different things,
/// which a single site-wide list cannot express. Choosing the roles a purchase puts its buyer in is assigning roles, so
/// only someone who may assign roles to users changes them; anyone else who edits the plan keeps the roles it has. The
/// editor offers only roles a subscription may grant: never a system role, the administrator role or a role holding a
/// security-critical permission (<see cref="IRoleGrantPolicy"/>).
/// </remarks>
public sealed class SubscriptionEntitlementPartDisplayDriver : ContentPartDisplayDriver<SubscriptionEntitlementPart>
{
    private readonly IRoleService _roleService;
    private readonly IRoleGrantPolicy _roleGrantPolicy;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionEntitlementPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="roleService">The role service used to list the roles a plan can grant.</param>
    /// <param name="roleGrantPolicy">The policy that says which roles a subscription may grant.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, for the user editing the plan.</param>
    public SubscriptionEntitlementPartDisplayDriver(
        IRoleService roleService,
        IRoleGrantPolicy roleGrantPolicy,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _roleService = roleService;
        _roleGrantPolicy = roleGrantPolicy;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(SubscriptionEntitlementPart part, BuildPartEditorContext context)
        => Initialize<SubscriptionEntitlementPartViewModel>(GetEditorShapeType(context), async model =>
        {
            var selected = new HashSet<string>(part.RoleNames ?? [], StringComparer.OrdinalIgnoreCase);

            model.Roles = [.. (await GetRolesAsync())
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

        if (!await CanChangeRolesAsync())
        {
            return Edit(part, context);
        }

        var available = await GetRolesAsync();

        part.RoleNames = [.. (model.Roles ?? [])
            .Where(role => role.IsSelected && !string.IsNullOrWhiteSpace(role.RoleName))
            .Select(role => role.RoleName.Trim())
            .Where(roleName => available.Contains(roleName, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        return Edit(part, context);
    }

    private async Task<bool> CanChangeRolesAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return user is not null && await _authorizationService.AuthorizeAsync(user, UsersPermissions.AssignRoleToUsers);
    }

    private async Task<IReadOnlyList<string>> GetRolesAsync()
    {
        var roles = new List<string>();

        foreach (var role in (await _roleService.GetRolesAsync()).Select(role => role.RoleName).Where(roleName => !string.IsNullOrEmpty(roleName)))
        {
            if (await _roleGrantPolicy.IsGrantableAsync(role) && !await _roleGrantPolicy.IsSecurityCriticalAsync(role))
            {
                roles.Add(role);
            }
        }

        return [.. roles.Order(StringComparer.OrdinalIgnoreCase)];
    }
}

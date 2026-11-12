using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Memberships.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Security.Services;
using OrchardCore.Settings;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Memberships.Drivers;

/// <summary>
/// Settings › Commerce › Memberships: the roles a membership purchase may grant. Choosing them is assigning roles, so
/// only someone who may manage memberships and assign roles to users sees or changes them.
/// </summary>
public sealed class MembershipSettingsDisplayDriver : SiteDisplayDriver<MembershipSettings>
{
    private readonly IRoleService _roleService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MembershipSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="roleService">The role service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public MembershipSettingsDisplayDriver(
        IRoleService roleService,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _roleService = roleService;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc />
    protected override string SettingsGroupId
        => MembershipsConstants.SettingsGroupId;

    /// <inheritdoc />
    public override async Task<IDisplayResult> EditAsync(ISite site, MembershipSettings settings, BuildEditorContext context)
    {
        if (!await IsAuthorizedAsync())
        {
            return null;
        }

        var roleNames = await GetRolesAsync();

        return Initialize<MembershipSettingsViewModel>("MembershipSettings_Edit", model =>
        {
            model.AvailableRoles = roleNames;
            model.GrantableRoles = settings.GrantableRoles ?? [];
        }).Location("Content:1")
        .OnGroup(SettingsGroupId);
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(ISite site, MembershipSettings settings, UpdateEditorContext context)
    {
        if (!await IsAuthorizedAsync())
        {
            return null;
        }

        var model = new MembershipSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var roleNames = await GetRolesAsync();

        settings.GrantableRoles = [.. roleNames.Where(role => (model.GrantableRoles ?? []).Contains(role, StringComparer.OrdinalIgnoreCase))];

        return await EditAsync(site, settings, context);
    }

    private async Task<bool> IsAuthorizedAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return user is not null &&
            await _authorizationService.AuthorizeAsync(user, MembershipsPermissions.ManageMemberships) &&
            await _authorizationService.AuthorizeAsync(user, UsersPermissions.AssignRoleToUsers);
    }

    private async Task<IReadOnlyList<string>> GetRolesAsync()
        => [.. (await _roleService.GetRoleNamesAsync()).Where(RoleGrantRules.IsGrantable).Order(StringComparer.OrdinalIgnoreCase)];
}

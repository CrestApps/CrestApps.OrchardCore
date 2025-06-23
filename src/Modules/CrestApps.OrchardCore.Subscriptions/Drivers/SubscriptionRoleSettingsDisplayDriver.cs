using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Security.Services;
using OrchardCore.Settings;
using OrchardCore.Users.ViewModels;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class SubscriptionRoleSettingsDisplayDriver : SiteDisplayDriver<SubscriptionRoleSettings>
{
    private readonly IRoleService _roleService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    protected override string SettingsGroupId
        => SubscriptionSettingsDisplayDriver.GroupId;

    public SubscriptionRoleSettingsDisplayDriver(
        IRoleService roleService,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _roleService = roleService;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override async Task<IDisplayResult> EditAsync(ISite model, SubscriptionRoleSettings settings, BuildEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, SubscriptionPermissions.ManageSubscriptionSettings))
        {
            return null;
        }

        var roleNames = await _roleService.GetRoleNamesAsync();

        return Initialize<SubscriptionRoleSettingsViewModel>("SubscriptionRoleSettings_Edit", model =>
        {
            var roleEntries = new List<RoleEntry>();
            foreach (var roleName in roleNames)
            {
                var roleEntry = new RoleEntry
                {
                    Role = roleName,
                    IsSelected = settings.RoleNames?.Contains(roleName, StringComparer.OrdinalIgnoreCase) ?? false,
                };

                roleEntries.Add(roleEntry);
            }

            model.Roles = roleEntries.ToArray();
        }).Location("Content:10")
        .RenderWhen(() => Task.FromResult(roleNames.Any()))
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, SubscriptionRoleSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, SubscriptionPermissions.ManageSubscriptionSettings))
        {
            return null;
        }

        var model = new SubscriptionRoleSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var roleNames = await _roleService.GetRoleNamesAsync();

        var selectedRoleNames = model.Roles.Where(role => role.IsSelected)
            .Select(role => role.Role)
            .ToArray();

        settings.RoleNames = roleNames.Where(roleName => selectedRoleNames.Contains(roleName)).ToArray();

        return await EditAsync(site, settings, context);
    }
}

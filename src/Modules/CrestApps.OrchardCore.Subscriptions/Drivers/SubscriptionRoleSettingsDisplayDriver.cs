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

/// <summary>
/// Provides the site settings editor for selecting roles assigned to completed subscription users.
/// </summary>
public sealed class SubscriptionRoleSettingsDisplayDriver : SiteDisplayDriver<SubscriptionRoleSettings>
{
    private readonly IRoleService _roleService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Gets the settings group used to render subscription settings.
    /// </summary>
    protected override string SettingsGroupId
        => SubscriptionSettingsDisplayDriver.GroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionRoleSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="roleService">The role service used to load available role names.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor used to read the current user.</param>
    /// <param name="authorizationService">The authorization service used to check access to subscription settings.</param>
    public SubscriptionRoleSettingsDisplayDriver(
        IRoleService roleService,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _roleService = roleService;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Builds the editor shape for subscription role settings when the current user is authorized.
    /// </summary>
    /// <param name="model">The site being edited.</param>
    /// <param name="settings">The subscription role settings to display.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The display result for the role settings editor, or <see langword="null"/> when access is denied.</returns>
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

    /// <summary>
    /// Updates the selected subscription roles from the posted site settings values.
    /// </summary>
    /// <param name="site">The site being updated.</param>
    /// <param name="settings">The subscription role settings to update.</param>
    /// <param name="context">The editor update context.</param>
    /// <returns>The updated editor display result, or <see langword="null"/> when access is denied.</returns>
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

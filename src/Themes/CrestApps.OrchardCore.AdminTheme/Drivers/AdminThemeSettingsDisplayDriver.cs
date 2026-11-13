using CrestApps.OrchardCore.AdminTheme.Models;
using CrestApps.OrchardCore.AdminTheme.Services;
using CrestApps.OrchardCore.AdminTheme.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AdminTheme.Drivers;

/// <summary>
/// Shows and saves the admin theme's site settings on the Settings → Admin page, beside Orchard Core's own admin settings.
/// </summary>
public sealed class AdminThemeSettingsDisplayDriver : SiteDisplayDriver<AdminThemeSettings>
{
    /// <summary>
    /// The settings group of Orchard Core's admin settings ('AdminSiteSettingsDisplayDriver.GroupId').
    /// </summary>
    public const string GroupId = "admin";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly AdminColorSchemeService _colorSchemes;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminThemeSettingsDisplayDriver"/> class.
    /// </summary>
    public AdminThemeSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, AdminColorSchemeService colorSchemes, IStringLocalizer<AdminThemeSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _colorSchemes = colorSchemes;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId => GroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, AdminThemeSettings settings, BuildEditorContext context)
        => Initialize<AdminThemeSettingsViewModel>("AdminThemeSettings_Edit", async model =>
        {
            model.DefaultColorScheme = await _colorSchemes.FindAsync(settings.DefaultColorScheme) ?? await _colorSchemes.GetSiteSchemeAsync();
            model.Schemes = await _colorSchemes.ListAsync();
        }).Location("Content:4")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, AdminPermissions.ManageAdminSettings))
        .OnGroup(SettingsGroupId);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, AdminThemeSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, AdminPermissions.ManageAdminSettings))
        {
            return null;
        }

        var model = new AdminThemeSettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var scheme = await _colorSchemes.FindAsync(model.DefaultColorScheme);

        if (scheme is null)
        {
            context.Updater.ModelState.AddModelError($"{Prefix}.{nameof(model.DefaultColorScheme)}", S["Pick one of the color schemes."]);
        }
        else
        {
            settings.DefaultColorScheme = scheme;
        }

        return Edit(site, settings, context);
    }
}

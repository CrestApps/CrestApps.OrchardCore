using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.DncRegistry.Drivers;

/// <summary>
/// Site settings display driver for configuring the USA FTC DNC Registry API connection.
/// </summary>
public sealed class UsaFtcDncRegistrySettingsDisplayDriver : SiteDisplayDriver<UsaFtcDncRegistrySettings>
{
    private const string ProtectorPurpose = "CrestApps.OrchardCore.DncRegistry.UsaFtcSettings";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsaFtcDncRegistrySettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public UsaFtcDncRegistrySettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<UsaFtcDncRegistrySettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => DncRegistryConstants.SettingsGroupIds.UsaFtcRegistry;

    public override IDisplayResult Edit(ISite site, UsaFtcDncRegistrySettings settings, BuildEditorContext context)
    {
        return Initialize<UsaFtcDncRegistrySettingsViewModel>("UsaFtcDncRegistrySettings_Edit", viewModel =>
        {
            viewModel.HasApiKey = !string.IsNullOrWhiteSpace(settings.ProtectedApiKey);
            viewModel.OrganizationId = settings.OrganizationId;
            viewModel.BaseUrl = settings.BaseUrl;
        }).Location("Content:5")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(
            _httpContextAccessor.HttpContext?.User,
            DncRegistryPermissions.ManageDncRegistrySettings));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, UsaFtcDncRegistrySettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, DncRegistryPermissions.ManageDncRegistrySettings))
        {
            return null;
        }

        var viewModel = new UsaFtcDncRegistrySettingsViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        settings.OrganizationId = viewModel.OrganizationId;
        settings.BaseUrl = viewModel.BaseUrl;

        if (string.IsNullOrWhiteSpace(settings.OrganizationId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.OrganizationId), S["Organization ID is required."]);
        }

        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.BaseUrl), S["Base URL is required."]);
        }

        if (!string.IsNullOrWhiteSpace(viewModel.ApiKey))
        {
            var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);
            settings.ProtectedApiKey = protector.Protect(viewModel.ApiKey);
        }
        else if (string.IsNullOrWhiteSpace(settings.ProtectedApiKey))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.ApiKey), S["API key is required."]);
        }

        return Edit(site, settings, context);
    }
}

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
/// Site settings display driver for configuring the Canada LNNTE-DNCL Registry API connection.
/// </summary>
public sealed class CanadaDnclRegistrySettingsDisplayDriver : SiteDisplayDriver<CanadaDnclRegistrySettings>
{
    private const string ProtectorPurpose = "CrestApps.OrchardCore.DncRegistry.CanadaDnclSettings";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CanadaDnclRegistrySettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public CanadaDnclRegistrySettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<CanadaDnclRegistrySettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => DncRegistryConstants.SettingsGroupIds.CanadaDnclRegistry;

    public override IDisplayResult Edit(ISite site, CanadaDnclRegistrySettings settings, BuildEditorContext context)
    {
        return Initialize<CanadaDnclRegistrySettingsViewModel>("CanadaDnclRegistrySettings_Edit", viewModel =>
        {
            viewModel.HasApiKey = !string.IsNullOrWhiteSpace(settings.ProtectedApiKey);
            viewModel.AccountNumber = settings.AccountNumber;
            viewModel.BaseUrl = settings.BaseUrl;
        }).Location("Content:10")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(
            _httpContextAccessor.HttpContext?.User,
            DncRegistryPermissions.ManageDncRegistrySettings));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, CanadaDnclRegistrySettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, DncRegistryPermissions.ManageDncRegistrySettings))
        {
            return null;
        }

        var viewModel = new CanadaDnclRegistrySettingsViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        settings.AccountNumber = viewModel.AccountNumber;
        settings.BaseUrl = viewModel.BaseUrl;

        if (string.IsNullOrWhiteSpace(settings.AccountNumber))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.AccountNumber), S["Account number is required."]);
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

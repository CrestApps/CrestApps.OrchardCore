using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.DncRegistry.Drivers;

/// <summary>
/// Site settings display driver for configuring global DNC registry enforcement.
/// </summary>
public sealed class DncRegistrySettingsDisplayDriver : SiteDisplayDriver<DncRegistrySettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<INationalDoNotCallRegistry> _registries;

    /// <summary>
    /// Initializes a new instance of the <see cref="DncRegistrySettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="registries">The available do-not-call registries.</param>
    public DncRegistrySettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IEnumerable<INationalDoNotCallRegistry> registries)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _registries = registries;
    }

    protected override string SettingsGroupId
        => DncRegistryConstants.SettingsGroupIds.ImportContentSettings;

    public override IDisplayResult Edit(ISite site, DncRegistrySettings settings, BuildEditorContext context)
    {
        return Initialize<DncRegistrySettingsViewModel>("DncRegistrySettings_Edit", viewModel =>
        {
            viewModel.EnforceGlobally = settings.EnforceGlobally;
            viewModel.EnforcedRegistryKeys = settings.EnforcedRegistryKeys ?? [];
            viewModel.AvailableRegistries = _registries.Select(r => new RegistryEntry
            {
                Key = r.Key,
                DisplayName = r.DisplayName,
                Description = r.Description,
            }).ToArray();
        }).Location("Content:1")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(
            _httpContextAccessor.HttpContext?.User,
            DncRegistryPermissions.ManageDncRegistrySettings));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, DncRegistrySettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, DncRegistryPermissions.ManageDncRegistrySettings))
        {
            return null;
        }

        var viewModel = new DncRegistrySettingsViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        settings.EnforceGlobally = viewModel.EnforceGlobally;
        settings.EnforcedRegistryKeys = viewModel.EnforcedRegistryKeys ?? [];

        return Edit(site, settings, context);
    }
}

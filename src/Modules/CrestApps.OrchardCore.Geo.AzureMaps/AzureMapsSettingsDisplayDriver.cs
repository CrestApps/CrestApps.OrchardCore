using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Geo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Geo.AzureMaps;

/// <summary>
/// Adds the Azure Maps key to the Maps and Geocoding settings.
/// </summary>
public sealed class AzureMapsSettingsDisplayDriver : SiteDisplayDriver<AzureMapsSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureMapsSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    public AzureMapsSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, IDataProtectionProvider dataProtectionProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId => GeoConstants.SettingsGroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, AzureMapsSettings settings, BuildEditorContext context)
        => Initialize<AzureMapsSettingsViewModel>("AzureMapsSettings_Edit", model => model.HasKey = ProtectedSettingExtensions.HasSecret(settings.Key))
            .Location("Content:5")
            .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, GeoPermissions.ManageGeoSettings))
            .OnGroup(SettingsGroupId);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, AzureMapsSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, GeoPermissions.ManageGeoSettings))
        {
            return null;
        }

        var model = new AzureMapsSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.Key = _dataProtectionProvider.ApplySecret(AzureMapsSettings.ProtectionPurpose, settings.Key, model.Key, model.RemoveKey);


        return Edit(site, settings, context);
    }
}

/// <summary>
/// The Azure Maps key editor.
/// </summary>
public class AzureMapsSettingsViewModel
{
    /// <summary>
    /// Gets or sets a new key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to remove the key.
    /// </summary>
    public bool RemoveKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a key is saved.
    /// </summary>
    [BindNever]
    public bool HasKey { get; set; }
}

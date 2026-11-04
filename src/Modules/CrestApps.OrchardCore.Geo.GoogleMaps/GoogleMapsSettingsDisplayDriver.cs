using CrestApps.OrchardCore.Geo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Geo.GoogleMaps;

/// <summary>
/// Adds the Google Maps key to the Maps and Geocoding settings.
/// </summary>
public sealed class GoogleMapsSettingsDisplayDriver : SiteDisplayDriver<GoogleMapsSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtector _protector;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleMapsSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    public GoogleMapsSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, IDataProtectionProvider dataProtectionProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _protector = dataProtectionProvider.CreateProtector(GoogleMapsSettings.ProtectionPurpose);
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId => GeoConstants.SettingsGroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, GoogleMapsSettings settings, BuildEditorContext context)
        => Initialize<GoogleMapsSettingsViewModel>("GoogleMapsSettings_Edit", model => model.HasKey = !string.IsNullOrEmpty(settings.Key))
            .Location("Content:5")
            .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, GeoPermissions.ManageGeoSettings))
            .OnGroup(SettingsGroupId);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, GoogleMapsSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, GeoPermissions.ManageGeoSettings))
        {
            return null;
        }

        var model = new GoogleMapsSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.RemoveKey)
        {
            settings.Key = null;
        }
        else if (!string.IsNullOrWhiteSpace(model.Key))
        {
            settings.Key = _protector.Protect(model.Key.Trim());
        }

        return Edit(site, settings, context);
    }
}

/// <summary>
/// The Google Maps key editor.
/// </summary>
public class GoogleMapsSettingsViewModel
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

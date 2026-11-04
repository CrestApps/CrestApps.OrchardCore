using CrestApps.OrchardCore.Geo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Geo.Osrm;

/// <summary>
/// Adds the OSRM server to the Maps and Geocoding settings.
/// </summary>
public sealed class OsrmSettingsDisplayDriver : SiteDisplayDriver<OsrmSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OsrmSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OsrmSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, IStringLocalizer<OsrmSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId => GeoConstants.SettingsGroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, OsrmSettings settings, BuildEditorContext context)
        => Initialize<OsrmSettingsViewModel>("OsrmSettings_Edit", model =>
            {
                model.BaseUrl = settings.BaseUrl;
                model.Profile = settings.Profile;
            })
            .Location("Content:5")
            .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, GeoPermissions.ManageGeoSettings))
            .OnGroup(SettingsGroupId);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, OsrmSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, GeoPermissions.ManageGeoSettings))
        {
            return null;
        }

        var model = new OsrmSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var url = model.BaseUrl?.Trim().TrimEnd('/');

        if (!string.IsNullOrEmpty(url) && (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.BaseUrl), S["The OSRM server's address must start with http:// or https://."]);
        }
        else
        {
            settings.BaseUrl = string.IsNullOrEmpty(url) ? null : url;
            settings.Profile = string.IsNullOrWhiteSpace(model.Profile) ? "driving" : model.Profile.Trim();
        }

        return Edit(site, settings, context);
    }
}

/// <summary>
/// The OSRM settings editor.
/// </summary>
public class OsrmSettingsViewModel
{
    /// <summary>
    /// Gets or sets the server's address.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the routing profile.
    /// </summary>
    public string Profile { get; set; }
}

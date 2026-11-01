using CrestApps.OrchardCore.Geo.Core.Indexes;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.Geo.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Geo.Drivers;

/// <summary>
/// Edits the map, geocoding and travel-time settings.
/// </summary>
public sealed class GeoSettingsDisplayDriver : SiteDisplayDriver<GeoSettings>
{
    /// <summary>
    /// The settings group.
    /// </summary>
    public const string GroupId = "geo";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<IGeocodingProvider> _geocoders;
    private readonly IEnumerable<ITravelTimeProvider> _travelProviders;
    private readonly IEnumerable<IRouteProvider> _routeProviders;
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="geocoders">The geocoding providers.</param>
    /// <param name="travelProviders">The travel-time providers.</param>
    /// <param name="routeProviders">The route providers.</param>
    /// <param name="session">The session.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public GeoSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IEnumerable<IGeocodingProvider> geocoders,
        IEnumerable<ITravelTimeProvider> travelProviders,
        IEnumerable<IRouteProvider> routeProviders,
        ISession session,
        IStringLocalizer<GeoSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _geocoders = geocoders;
        _travelProviders = travelProviders;
        _routeProviders = routeProviders;
        _session = session;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId => GroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, GeoSettings settings, BuildEditorContext context)
        => Initialize<GeoSettingsViewModel>("GeoSettings_Edit", async model =>
        {
            var requests = await _session.QueryIndex<GeocodeRequestIndex>(collection: GeoConstants.CollectionName).ListAsync();

            model.Waiting = requests.Count(index => index.Status == nameof(GeocodeRequestStatus.Pending));
            model.Done = requests.Count(index => index.Status == nameof(GeocodeRequestStatus.Done));
            model.Failed = requests.Count(index => index.Status == nameof(GeocodeRequestStatus.Failed));

            model.GeocodingProvider = settings.GeocodingProvider;
            model.TravelTimeProvider = settings.TravelTimeProvider;
            model.RouteProvider = settings.RouteProvider;
            model.AverageSpeedKmh = settings.AverageSpeedKmh;
            model.RoadFactor = settings.RoadFactor;
            model.MatrixCacheMinutes = settings.MatrixCacheMinutes;
            model.TileUrl = settings.TileUrl;
            model.TileAttribution = settings.TileAttribution;
            model.DefaultLatitude = settings.DefaultLatitude;
            model.DefaultLongitude = settings.DefaultLongitude;
            model.DefaultZoom = settings.DefaultZoom;
            model.GeocodingProviders = Options(_geocoders.Select(provider => provider.Name), settings.GeocodingProvider, S["None: addresses aren't geocoded"]);
            model.TravelTimeProviders = Options(_travelProviders.Select(provider => provider.Name).Where(name => name != GeoConstants.HaversineProviderName), settings.TravelTimeProvider, S["Built-in estimate"]);
            model.RouteProviders = Options(_routeProviders.Select(provider => provider.Name).Where(name => name != GeoConstants.HaversineProviderName), settings.RouteProvider, S["Straight lines"]);
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, GeoPermissions.ManageGeoSettings))
        .OnGroup(SettingsGroupId);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, GeoSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, GeoPermissions.ManageGeoSettings))
        {
            return null;
        }

        var model = new GeoSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.AverageSpeedKmh is <= 0 or > 200)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.AverageSpeedKmh), S["The average speed must be more than 0 and at most 200 km/h."]);
        }

        if (model.RoadFactor is < 1 or > 3)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.RoadFactor), S["The road factor must be from 1 to 3."]);
        }

        if (string.IsNullOrWhiteSpace(model.TileUrl) || !model.TileUrl.Contains("{z}", StringComparison.Ordinal) || !model.TileUrl.Contains("{x}", StringComparison.Ordinal) || !model.TileUrl.Contains("{y}", StringComparison.Ordinal))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.TileUrl), S["The tile address needs {0}, {1} and {2}.", "{z}", "{x}", "{y}"]);
        }

        if (model.DefaultLatitude is < -90 or > 90 || model.DefaultLongitude is < -180 or > 180 || model.DefaultZoom is < 1 or > 19)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.DefaultLatitude), S["The map's starting point must be a valid latitude and longitude, and its zoom from 1 to 19."]);
        }

        if (context.Updater.ModelState.IsValid)
        {
            settings.GeocodingProvider = Blank(model.GeocodingProvider);
            settings.TravelTimeProvider = Blank(model.TravelTimeProvider);
            settings.RouteProvider = Blank(model.RouteProvider);
            settings.AverageSpeedKmh = model.AverageSpeedKmh;
            settings.RoadFactor = model.RoadFactor;
            settings.MatrixCacheMinutes = Math.Clamp(model.MatrixCacheMinutes, 1, 10_080);
            settings.TileUrl = model.TileUrl.Trim();
            settings.TileAttribution = model.TileAttribution?.Trim();
            settings.DefaultLatitude = model.DefaultLatitude;
            settings.DefaultLongitude = model.DefaultLongitude;
            settings.DefaultZoom = model.DefaultZoom;
        }

        return Edit(site, settings, context);
    }

    private static string Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static List<SelectListItem> Options(IEnumerable<string> names, string selected, string none)
        => [new SelectListItem(none, string.Empty, string.IsNullOrEmpty(selected)), .. names.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).Select(name => new SelectListItem(name, name, string.Equals(name, selected, StringComparison.OrdinalIgnoreCase)))];
}

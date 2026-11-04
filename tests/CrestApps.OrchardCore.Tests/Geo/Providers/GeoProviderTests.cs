using CrestApps.OrchardCore.Geo.AzureMaps;
using CrestApps.OrchardCore.Geo.Core;
using CrestApps.OrchardCore.Geo.GoogleMaps;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Osrm;
using Microsoft.AspNetCore.DataProtection;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Geo.Providers;

/// <summary>
/// Azure Maps, Google Maps and OSRM each pass the provider contract against recorded answers, send their key or
/// address the way the service expects, and report refusals.
/// </summary>
public sealed class GeoProviderTests
{
    private static readonly EphemeralDataProtectionProvider _protection = new EphemeralDataProtectionProvider();

    [Fact]
    public async Task AzureMaps_PassesTheContract()
    {
        var http = new RecordedHttp()
            .Answer("geocode?", "azure-geocode.json")
            .Answer("route/matrix", "azure-matrix.json")
            .Answer("route/directions", "azure-route.json");
        var provider = Azure(http, "azure-key");

        await GeoProviderContract.GeocodesAsync(provider);
        await GeoProviderContract.GivesAMatrixAsync(provider);
        await GeoProviderContract.GivesARouteAsync(provider);

        Assert.All(http.Requests, request => Assert.Equal("azure-key", Assert.Single(request.Headers.GetValues("subscription-key"))));
        Assert.Contains("\"type\":\"MultiPoint\"", http.Bodies[1], StringComparison.Ordinal);
        Assert.Contains("departAt=now", http.Requests[1].RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AzureMaps_ReportsRefusalsAndEmptyAnswers()
    {
        await GeoProviderContract.FindsNothingAsync(Azure(new RecordedHttp().Answer("geocode?", "azure-empty.json"), "azure-key"));
        await GeoProviderContract.ReportsARefusalAsync(() => Azure(new RecordedHttp().Answer("geocode?", "azure-error.json", System.Net.HttpStatusCode.Unauthorized), "azure-key").GeocodeAsync(new GeocodeQuery { Text = "x" }), "The subscription key is invalid.");
        await GeoProviderContract.ReportsARefusalAsync(() => Azure(new RecordedHttp(), null).GeocodeAsync(new GeocodeQuery { Text = "x" }), "no subscription key");
    }

    [Fact]
    public async Task GoogleMaps_PassesTheContract_AndIsNeverCached()
    {
        var http = new RecordedHttp()
            .Answer("geocode/json", "google-geocode.json")
            .Answer("distancematrix", "google-matrix.json")
            .Answer("directions", "google-route.json");
        var provider = Google(http, "google-key");

        await GeoProviderContract.GeocodesAsync(provider);
        await GeoProviderContract.GivesAMatrixAsync(provider);

        var route = await provider.GetRouteAsync([GeoProviderContract.CityHall, GeoProviderContract.Cambridge], TestContext.Current.CancellationToken);

        // The recorded polyline is the format's own example.
        Assert.Equal([new GeoPoint(38.5, -120.2), new GeoPoint(40.7, -120.95), new GeoPoint(43.252, -126.453)], route.Line);
        Assert.Equal(["Head north on City Hall Sq", "Turn left onto Cambridge St"], route.Instructions);
        Assert.All(http.Requests, request => Assert.Contains("key=google-key", request.RequestUri!.Query, StringComparison.Ordinal));
        Assert.Equal(TimeSpan.Zero, ((CrestApps.OrchardCore.Geo.Services.ITravelTimeProvider)provider).MaxCacheDuration);
    }

    [Fact]
    public async Task GoogleMaps_ReportsRefusalsAndEmptyAnswers()
    {
        await GeoProviderContract.FindsNothingAsync(Google(new RecordedHttp().Answer("geocode/json", "google-empty.json"), "google-key"));
        await GeoProviderContract.ReportsARefusalAsync(() => Google(new RecordedHttp().Answer("geocode/json", "google-error.json"), "google-key").GeocodeAsync(new GeocodeQuery { Text = "x" }), "REQUEST_DENIED: The provided API key is invalid.");
        await GeoProviderContract.ReportsARefusalAsync(() => Google(new RecordedHttp(), null).GeocodeAsync(new GeocodeQuery { Text = "x" }), "no API key");
    }

    [Fact]
    public async Task Osrm_PassesTheContract()
    {
        var http = new RecordedHttp()
            .Answer("/table/v1/driving/", "osrm-table.json")
            .Answer("/route/v1/driving/", "osrm-route.json");
        var provider = Osrm(http, "http://osrm.local:5000/");

        await GeoProviderContract.GivesAMatrixAsync(provider);
        await GeoProviderContract.GivesARouteAsync(provider);

        Assert.Equal("http://osrm.local:5000/table/v1/driving/-71.058,42.3603;-71.058,42.3603;-71.1097,42.3736?sources=0&destinations=1;2&annotations=duration,distance", http.Requests[0].RequestUri!.ToString());
        Assert.Equal(["Head out on City Hall Square", "Turn left onto Cambridge Street", "Arrive"], (await provider.GetRouteAsync([GeoProviderContract.CityHall, GeoProviderContract.Cambridge], TestContext.Current.CancellationToken)).Instructions);
    }

    [Fact]
    public async Task Osrm_ReportsRefusals()
    {
        await GeoProviderContract.ReportsARefusalAsync(() => Osrm(new RecordedHttp().Answer("/route/", "osrm-error.json", System.Net.HttpStatusCode.BadRequest), "http://osrm.local").GetRouteAsync([GeoProviderContract.CityHall, GeoProviderContract.Cambridge]), "Impossible route between points");
        await GeoProviderContract.ReportsARefusalAsync(() => Osrm(new RecordedHttp(), null).GetRouteAsync([GeoProviderContract.CityHall, GeoProviderContract.Cambridge]), "no server address");
    }

    [Fact]
    public void ThePolylineDecoder_ReadsTheFormatsExample()
    {
        Assert.Equal([new GeoPoint(38.5, -120.2), new GeoPoint(40.7, -120.95), new GeoPoint(43.252, -126.453)], PolylineEncoding.Decode("_p~iF~ps|U_ulLnnqC_mqNvxq`@"));
        Assert.Throws<FormatException>(() => PolylineEncoding.Decode("_p~iF~ps|U_"));
    }

    private static AzureMapsProvider Azure(RecordedHttp http, string key)
        => new(Factory(http, "https://atlas.microsoft.com/"), Site(new AzureMapsSettings { Key = key is null ? null : _protection.CreateProtector(AzureMapsSettings.ProtectionPurpose).Protect(key) }), _protection);

    private static GoogleMapsProvider Google(RecordedHttp http, string key)
        => new(Factory(http, "https://maps.googleapis.com/"), Site(new GoogleMapsSettings { Key = key is null ? null : _protection.CreateProtector(GoogleMapsSettings.ProtectionPurpose).Protect(key) }), _protection);

    private static OsrmProvider Osrm(RecordedHttp http, string baseUrl)
        => new(Factory(http, null), Site(new OsrmSettings { BaseUrl = baseUrl }));

    private static IHttpClientFactory Factory(RecordedHttp http, string baseAddress)
    {
        var factory = new Mock<IHttpClientFactory>();

        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => http.Client(baseAddress));

        return factory.Object;
    }

    private static ISiteService Site<T>(T settings)
        where T : class, new()
    {
        var site = new SiteSettings();
        var service = new Mock<ISiteService>();

        site.Put(settings);
        service.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site);

        return service.Object;
    }
}

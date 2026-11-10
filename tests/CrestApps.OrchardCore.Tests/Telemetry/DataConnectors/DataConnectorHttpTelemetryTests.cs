using System.Net;
using System.Net.Http;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Tests.DataConnectors;

namespace CrestApps.OrchardCore.Tests.Telemetry.DataConnectors;

/// <summary>
/// The HTTP client of data connectors counts its retries, the authenticator counts the tokens it serves, and the
/// network guard counts the connections it refuses, with bounded tags only. The instruments
/// are process-wide and other tests send requests at the same time, so these tests check that the measurements they
/// cause are there, not how many there are.
/// </summary>
public sealed class DataConnectorHttpTelemetryTests
{
    private const string TokenResponse = """{ "access_token": "token-1", "token_type": "Bearer", "expires_in": 3600 }""";

    [Fact]
    public async Task SendAsync_ThatRetriesAThrottledRequest_CountsTheRetry()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var calls = 0;
        var client = DataConnectorHttpTests.CreateClient(_ => ++calls < 2 ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) : StubHttpHandler.Json("[]"), out _);

        // Act
        using var response = await client.SendAsync(new HttpConnectionSettings { BaseUrl = "https://api.example.com" }, new FakeSecrets(), "key", HttpMethod.Get, new Uri("https://api.example.com/items"), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(capture.Measurements, measurement =>
            measurement.Instrument == "crestapps.dataconnector.http.retries" &&
            Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.ReasonTag], DataConnectorsTelemetry.RetryStatus429));
        Assert.Contains(capture.Measurements, measurement =>
            measurement.Instrument == "crestapps.retry.attempts" &&
            Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.AreaTag], DataConnectorsTelemetry.RetryArea) &&
            Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.ReasonTag], DataConnectorsTelemetry.RetryStatus429) &&
            Equals(measurement.Tags["attempt"], 2));
        Assert.Empty(FindBannedTagKeys(capture));
    }

    [Fact]
    public async Task ApplyAsync_WithOAuth_CountsTheTokenItAcquires_ThenTheCacheHit()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var (authenticator, _) = DataConnectorHttpTests.CreateAuthenticator(_ => StubHttpHandler.Json(TokenResponse));
        var settings = new HttpAuthenticationSettings { Type = HttpAuthenticationType.OAuth2, TokenUrl = "https://login.example.com/token", ClientId = "app" };
        var secrets = DataConnectorHttpTests.Secrets((HttpAuthenticationSecrets.ClientSecret, "secret"));
        var cacheKey = Guid.NewGuid().ToString("n");

        // Act
        using (var first = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/a"))
        {
            await authenticator.ApplyAsync(first, settings, secrets, cacheKey, TestContext.Current.CancellationToken);
        }

        using (var second = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/b"))
        {
            await authenticator.ApplyAsync(second, settings, secrets, cacheKey, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Contains(capture.Measurements, measurement => IsTokenRequest(measurement, "acquired"));
        Assert.Contains(capture.Measurements, measurement => IsTokenRequest(measurement, "cache_hit"));
        Assert.Empty(FindBannedTagKeys(capture));
    }

    [Fact]
    public async Task ResolveAllowedAsync_ForAHostOnTheSiteNetwork_CountsTheRefusedConnection()
    {
        // Arrange
        using var capture = new TelemetryCapture();

        // Act
        await Assert.ThrowsAsync<DataConnectorException>(() =>
            DataConnectorNetwork.ResolveAllowedAsync("127.0.0.1", new DataConnectorNetworkOptions(), TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.dataconnector.network.blocked");
        Assert.Empty(FindBannedTagKeys(capture));
    }

    private static bool IsTokenRequest(CapturedMeasurement measurement, string result)
    {
        return measurement.Instrument == "crestapps.dataconnector.token.requests" &&
            Equals(measurement.Tags[DataConnectorsTelemetry.AuthTag], "oauth2") &&
            Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], result);
    }

    // Only this area's meter is checked, so a banned tag another area records doesn't fail these tests.
    private static List<string> FindBannedTagKeys(TelemetryCapture capture)
    {
        return capture.Measurements
            .Where(measurement => measurement.Meter == DataConnectorsTelemetry.SourceName)
            .SelectMany(measurement => measurement.Tags.Keys)
            .Where(CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}

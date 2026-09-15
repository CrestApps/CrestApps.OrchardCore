using System.Net;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Reconciliation could only ever repair calls the platform already knew about. A call that was placed and then
/// lost — the process died between originating it and persisting the interaction — left a real person connected
/// to a platform with no record of them, and nothing would ever notice. Asking the provider what is actually up
/// on the connection is the only way to see those.
/// </summary>
public sealed class TelnyxActiveCallListingTests
{
    [Fact]
    public async Task ActiveCalls_AreListedForTheConfiguredConnection()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """
            {
              "data": [
                { "call_control_id": "call-1", "call_duration": 42, "client_state": "eyJhIjoxfQ==" },
                { "call_control_id": "call-2", "call_duration": 7 }
              ]
            }
            """);
        var client = CreateClient(handler);

        // Act
        var result = await client.ListActiveCallsAsync("connection-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("/v2/connections/connection-1/active_calls", handler.Requests[0].Path);
        Assert.Equal(["call-1", "call-2"], result.Calls.Select(call => call.CallControlId));
        Assert.Equal(42, result.Calls[0].DurationSeconds);
        Assert.Equal("eyJhIjoxfQ==", result.Calls[0].ClientState);
    }

    [Fact]
    public async Task TheListing_CarriesTheCursorForTheNextPage()
    {
        // Arrange
        // A busy connection has more calls than one page holds, and the orphans are as likely to be on the last
        // page as the first.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """
            {
              "data": [ { "call_control_id": "call-1" } ],
              "meta": { "next_page_token": "page-2" }
            }
            """);
        var client = CreateClient(handler);

        // Act
        var result = await client.ListActiveCallsAsync("connection-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("page-2", result.NextPageToken);
    }

    [Fact]
    public async Task AContinuedListing_AsksForTheNamedPage()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{ "data": [] }""");
        var client = CreateClient(handler);

        // Act
        await client.ListActiveCallsAsync("connection-1", "page-2", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("page[after]=page-2", handler.Requests[0].Path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusedListing_IsReportedRatherThanThrown()
    {
        // Arrange
        // This runs on a timer against a live tenant. A provider hiccup must not surface as an unhandled
        // exception in a background task.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.Unauthorized);
        var client = CreateClient(handler);

        // Act
        var result = await client.ListActiveCallsAsync("connection-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(result.Calls);
    }

    [Fact]
    public async Task AnEmptyConnection_ListsNothing()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{ "data": [] }""");
        var client = CreateClient(handler);

        // Act
        var result = await client.ListActiveCallsAsync("connection-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Empty(result.Calls);
        Assert.Null(result.NextPageToken);
    }

    private static TelnyxApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.telnyx.com/v2/"),
        };

        return new TelnyxApiClient(
            httpClient,
            new OptionsWrapper<TelnyxOptions>(new TelnyxOptions
            {
                ApiBaseUrl = "https://api.telnyx.com/v2/",
                ApiKey = "test-api-key",
            }),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);
    }
}

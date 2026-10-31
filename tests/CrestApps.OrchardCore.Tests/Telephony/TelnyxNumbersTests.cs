using System.Net;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class TelnyxNumbersTests
{
    [Fact]
    public async Task Search_SendsTheFilters_AndReadsTheNumbers()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """
            {
              "data": [
                {
                  "phone_number": "+17025550123",
                  "region_information": [ { "region_type": "state", "region_name": "NV" }, { "region_type": "rate_center", "region_name": "LAS VEGAS" } ],
                  "cost_information": { "monthly_cost": "1.00", "upfront_cost": "1.00", "currency": "USD" },
                  "features": [ { "name": "voice" }, { "name": "sms" } ]
                }
              ]
            }
            """);
        var client = CreateClient(handler);

        // Act
        var (result, numbers) = await client.SearchAvailableNumbersAsync(new TelnyxNumberSearch { AreaCode = "702", Features = ["voice", "sms"], Limit = 5 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var path = Uri.UnescapeDataString(handler.Requests[0].Path);
        Assert.StartsWith("/v2/available_phone_numbers?", path, StringComparison.Ordinal);
        Assert.Contains("filter[country_code]=US", path, StringComparison.Ordinal);
        Assert.Contains("filter[national_destination_code]=702", path, StringComparison.Ordinal);
        Assert.Contains("filter[features][]=sms", path, StringComparison.Ordinal);
        Assert.Contains("filter[limit]=5", path, StringComparison.Ordinal);
        var number = Assert.Single(numbers);
        Assert.Equal("+17025550123", number.PhoneNumber);
        Assert.Equal("NV, LAS VEGAS", number.Region);
        Assert.Equal("1.00", number.MonthlyCost);
        Assert.Equal(["voice", "sms"], number.Features);
    }

    [Fact]
    public async Task Order_SendsTheNumbersOnTheConfiguredConnection_AndIsNeverRetried()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        // Act
        var (result, _, _) = await client.OrderNumbersAsync(["+17025550123"], cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v2/number_orders", request.Path);
        Assert.Contains("\"phone_number\":\"+17025550123\"", System.Text.RegularExpressions.Regex.Unescape(request.Body), StringComparison.Ordinal);
        Assert.Contains("\"connection_id\":\"conn-1\"", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerName_IsListedWithWhatCarriersAllow()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{ "data": {} }""");
        var client = CreateClient(handler);

        // Act
        await client.SetCallerNameAsync("num-1", "Acme Plumbing & Heating!", TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal("/v2/phone_numbers/num-1/voice", request.Path);
        Assert.Contains("\"cnam_listing_details\":\"Acme Plumbing\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"cnam_listing_enabled\":true", request.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Acme", "Acme")]
    [InlineData("  Acme & Sons, LLC ", "Acme  Sons LLC")]
    [InlineData("A Very Long Business Name", "A Very Long Bus")]
    [InlineData(null, "")]
    public void NormalizeCallerName_KeepsWhatCarriersAllow(string name, string expected)
        => Assert.Equal(expected, TelnyxApiClient.NormalizeCallerName(name));

    private static TelnyxApiClient CreateClient(HttpMessageHandler handler)
        => new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.telnyx.com/v2/") },
            new TestOptionsMonitor<TelnyxOptions>(new TelnyxOptions { ApiBaseUrl = "https://api.telnyx.com/v2/", ApiKey = "test-api-key", ConnectionId = "conn-1" }),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);
}

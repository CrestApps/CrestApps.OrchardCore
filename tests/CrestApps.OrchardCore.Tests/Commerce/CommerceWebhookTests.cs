using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Commerce.Webhooks;
using CrestApps.OrchardCore.Commerce.Webhooks.Controllers;
using CrestApps.OrchardCore.Commerce.Webhooks.Models;
using CrestApps.OrchardCore.Commerce.Webhooks.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// What a webhook receiver relies on: a signature it can check with its secret, a retry schedule that gives up, and
/// events sent only to the webhooks that asked for them; and what an API client relies on when it retries.
/// </summary>
public sealed class CommerceWebhookTests
{
    [Fact]
    public void TheSignature_IsTheHmacOfTheTimestampAndBody_KeyedWithTheSecret()
    {
        const string secret = "whsec_test";
        const string body = """{"id":"evt_1","event":"order.paid"}""";

        var signature = WebhookDispatcher.Sign(secret, 1_760_000_000, body);
        var expected = "v1=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes("1760000000." + body)));

        Assert.Equal(expected, signature);
        Assert.NotEqual(signature, WebhookDispatcher.Sign(secret, 1_760_000_001, body));
        Assert.NotEqual(signature, WebhookDispatcher.Sign("whsec_other", 1_760_000_000, body));
    }

    [Fact]
    public void AFailedDelivery_IsTriedLessAndLessOften_ThenGivenUp()
    {
        var delays = Enumerable.Range(1, 10).Select(WebhookDispatcher.GetRetryDelay).ToArray();

        Assert.Equal(TimeSpan.FromMinutes(1), delays[0]);
        Assert.True(delays.TakeWhile(delay => delay.HasValue).Zip(delays.Skip(1).TakeWhile(delay => delay.HasValue)).All(pair => pair.First < pair.Second));
        Assert.Null(delays[WebhookDispatcher.Backoff.Length]);
    }

    [Theory]
    [InlineData("order.paid", true)]
    [InlineData("order.canceled", false)]
    public void AWebhook_TakesTheEventsItAskedFor(string eventName, bool takes)
    {
        var subscription = new WebhookSubscription { Events = ["order.paid", "shipment.shipped"] };

        Assert.Equal(takes, subscription.Takes(eventName));
        Assert.True(new WebhookSubscription { Events = [WebhookConstants.AllEvents] }.Takes(eventName));
        Assert.False(new WebhookSubscription { Events = [WebhookConstants.AllEvents], IsEnabled = false }.Takes(eventName));
    }

    [Theory]
    [InlineData("https://hooks.example.com/orders", false, true)]
    [InlineData("http://hooks.example.com/orders", false, false)]
    [InlineData("http://localhost:8080/hook", false, false)]
    [InlineData("http://localhost:8080/hook", true, true)]
    [InlineData("http://127.0.0.1:8080/hook", true, true)]
    [InlineData("http://10.0.0.5/hook", true, false)]
    [InlineData("https://user:password@hooks.example.com/", false, false)]
    [InlineData("ftp://hooks.example.com/", false, false)]
    [InlineData("/relative", false, false)]
    [InlineData("https://10.0.0.5/hook", false, false)]
    [InlineData("https://169.254.169.254/latest", false, false)]
    [InlineData("https://192.168.1.20/hook", true, false)]
    [InlineData("https://localhost/hook", false, false)]
    public void Events_ArePostedOnlyOverHttps_OrToThisMachineWhileDeveloping(string url, bool isDevelopment, bool allowed)
        => Assert.Equal(allowed, WebhooksAdminController.IsAllowedUrl(url, isDevelopment));

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.20.0.1", true)]
    [InlineData("192.168.0.10", true)]
    [InlineData("169.254.169.254", true)]
    [InlineData("::1", true)]
    [InlineData("fd00::1", true)]
    [InlineData("::ffff:10.0.0.1", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("172.32.0.1", false)]
    public void AnAddressOfTheSitesOwnNetwork_IsInternal(string address, bool isInternal)
        => Assert.Equal(isInternal, WebhookNetwork.IsInternal(IPAddress.Parse(address)));

    [Fact]
    public async Task AHostOnTheSitesOwnNetwork_IsRefusedWhenCalled()
    {
        // A hostname passes when it's saved; the connection is what refuses it, whatever it resolves to.
        using var client = new HttpClient(WebhookNetwork.CreateHandler(allowLoopback: false));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://localhost:1/hook", TestContext.Current.CancellationToken));

        Assert.Contains("own network", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARetry_GetsBackTheGuestTokenAndLocation_TheFirstAnswerHadInItsHeaders()
    {
        var headers = new HeaderDictionary { [CommerceApi.CheckoutTokenHeader] = "token-1", ["X-Other"] = "not kept" };
        Results<Created<ApiMoney>, ProblemHttpResult> created = TypedResults.Created("/api/commerce/v1/checkout/1", new ApiMoney(5m, "USD"));

        var stored = IdempotencyEndpointFilter.Capture(created, headers);

        Assert.Equal(StatusCodes.Status201Created, stored.StatusCode);
        Assert.Equal("token-1", stored.Headers[CommerceApi.CheckoutTokenHeader]);
        Assert.Equal("/api/commerce/v1/checkout/1", stored.Headers["Location"]);
        Assert.False(stored.Headers.ContainsKey("X-Other"));
    }

    [Fact]
    public void OnlyAFinalAnswer_IsKeptForARetry()
    {
        Assert.True(IdempotencyEndpointFilter.IsFinal(IdempotencyEndpointFilter.Capture(TypedResults.Ok(new ApiMoney(1m, "USD")))));
        Assert.True(IdempotencyEndpointFilter.IsFinal(IdempotencyEndpointFilter.Capture(ApiProblems.Conflict(ApiProblems.Codes.OutOfStock, "Sold out."))));
        Assert.False(IdempotencyEndpointFilter.IsFinal(IdempotencyEndpointFilter.Capture(ApiProblems.Conflict(ApiProblems.Codes.Concurrency, "Changed elsewhere."))));
        Assert.False(IdempotencyEndpointFilter.IsFinal(IdempotencyEndpointFilter.Capture(ApiProblems.TooManyRequests("Slow down."))));
        Assert.False(IdempotencyEndpointFilter.IsFinal(IdempotencyEndpointFilter.Capture(TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable))));
        Assert.Equal("application/problem+json", IdempotencyEndpointFilter.Capture(ApiProblems.NotFound()).ContentType);
    }
}

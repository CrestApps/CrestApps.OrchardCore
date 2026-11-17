using System.Net;
using CrestApps.OrchardCore.Plaid;
using CrestApps.OrchardCore.Plaid.Models;
using CrestApps.OrchardCore.Plaid.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace CrestApps.OrchardCore.Tests.Plaid;

public sealed class PlaidClientTests
{
    [Fact]
    public async Task CreateLinkToken_AsksForAuthOnUnitedStatesCheckingAndSavings_WithTheCredentialsInHeadersOnly()
    {
        // Arrange
        var context = new PlaidTestContext();
        context.Handler.Answer("link/token/create", """{"link_token":"link-sandbox-1","request_id":"r"}""");

        // Act
        var token = await context.Client.CreateLinkTokenAsync(FixedPlaidCredentials.Sandbox, new PlaidLinkTokenRequest
        {
            ClientUserId = "opaque",
            ClientName = "A site with a very long name that Plaid would refuse",
            Language = "es",
            WebhookUrl = "https://example.com/plaid/webhook",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("link-sandbox-1", token.LinkToken);
        var (_, request, body) = Assert.Single(context.Handler.Calls);
        Assert.Equal("https://sandbox.plaid.com/link/token/create", request.RequestUri!.ToString());
        Assert.Equal("client-1", request.Headers.GetValues("PLAID-CLIENT-ID").Single());
        Assert.Equal("secret-1", request.Headers.GetValues("PLAID-SECRET").Single());
        Assert.DoesNotContain("secret-1", body.ToJsonString(), StringComparison.Ordinal);
        Assert.Equal("opaque", body["user"]!["client_user_id"]!.GetValue<string>());
        Assert.Equal("auth", Assert.Single(body["products"]!.AsArray())!.GetValue<string>());
        Assert.Equal("US", Assert.Single(body["country_codes"]!.AsArray())!.GetValue<string>());
        Assert.Equal(["checking", "savings"], body["account_filters"]!["depository"]!["account_subtypes"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Equal("https://example.com/plaid/webhook", body["webhook"]!.GetValue<string>());
        Assert.Equal("es", body["language"]!.GetValue<string>());
        Assert.True(body["client_name"]!.GetValue<string>().Length <= 30);
        Assert.True(request.Options.TryGetValue(PlaidClient.RetryableKey, out var retryable) && retryable);
    }

    [Fact]
    public async Task ExchangePublicToken_IsNeverMarkedRetryable()
    {
        // Arrange
        var context = new PlaidTestContext();
        context.Handler.Answer("item/public_token/exchange", """{"access_token":"access-1","item_id":"item-1"}""");

        // Act
        var exchange = await context.Client.ExchangePublicTokenAsync(FixedPlaidCredentials.Sandbox, "public-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("access-1", exchange.AccessToken);
        Assert.Equal("item-1", exchange.ItemId);
        Assert.DoesNotContain("access-1", exchange.ToString(), StringComparison.Ordinal);
        var (_, request, _) = Assert.Single(context.Handler.Calls);
        Assert.True(request.Options.TryGetValue(PlaidClient.RetryableKey, out var retryable));
        Assert.False(retryable);
    }

    [Fact]
    public async Task GetAuth_ReadsTheAchNumbersOfTheAccount()
    {
        // Arrange
        var context = new PlaidTestContext();
        context.Handler.Answer("auth/get", PlaidSamples.Auth("acc-1", "1111222233330000", "011401533", tokenized: false));

        // Act
        var auth = await context.Client.GetAuthAsync(FixedPlaidCredentials.Sandbox, "access-1", "acc-1", TestContext.Current.CancellationToken);

        // Assert
        var numbers = Assert.Single(auth.Numbers.Ach);
        Assert.Equal("1111222233330000", numbers.Account);
        Assert.Equal("011401533", numbers.Routing);
        Assert.False(numbers.IsTokenizedAccountNumber);
        Assert.Equal("checking", Assert.Single(auth.Accounts).Subtype);
        Assert.Equal("acc-1", Assert.Single(context.Handler.Calls).Body["options"]!["account_ids"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task AnError_IsReadIntoPlaidApiException_WithoutTheBody()
    {
        // Arrange
        var context = new PlaidTestContext();
        context.Handler.Error("auth/get", "ITEM_ERROR", "PRODUCT_NOT_READY", displayMessage: "Not yet.");

        // Act
        var exception = await Assert.ThrowsAsync<PlaidApiException>(() => context.Client.GetAuthAsync(FixedPlaidCredentials.Sandbox, "access-1", "acc-1", TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("PRODUCT_NOT_READY", exception.ErrorCode);
        Assert.Equal("Not yet.", exception.Error.DisplayMessage);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("req-1", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("access-1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_UsesTheProductionHost_AndCredentialsNeverPrintTheSecret()
    {
        // Arrange
        var credentials = new PlaidCredentials(PlaidEnvironment.Production, "client-1", "secret-1");

        // Act
        var text = credentials.ToString();

        // Assert
        Assert.Equal("https://production.plaid.com/", credentials.BaseAddress.ToString());
        Assert.DoesNotContain("secret-1", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resilience_RetriesAReadAfterATransientFailure_ButNeverTheExchange()
    {
        // Arrange
        var handler = new FakePlaidHandler()
            .Answer("auth/get", "{}", HttpStatusCode.ServiceUnavailable)
            .Answer("auth/get", PlaidSamples.Auth("acc-1", "1111222233330000", "011401533", tokenized: false))
            .Answer("item/public_token/exchange", "{}", HttpStatusCode.ServiceUnavailable)
            .Answer("item/public_token/exchange", """{"access_token":"access-1","item_id":"item-1"}""");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPlaidCore();
        services.AddHttpClient(PlaidConstants.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        services.Configure<HttpStandardResilienceOptions>(PlaidConstants.HttpClientName + "-standard", options => options.Retry.Delay = TimeSpan.Zero);

        using var provider = services.BuildServiceProvider();
        var client = new PlaidClient(provider.GetRequiredService<IHttpClientFactory>());

        // Act
        var auth = await client.GetAuthAsync(FixedPlaidCredentials.Sandbox, "access-1", "acc-1", TestContext.Current.CancellationToken);
        var exchange = await Assert.ThrowsAsync<PlaidApiException>(() => client.ExchangePublicTokenAsync(FixedPlaidCredentials.Sandbox, "public-1", TestContext.Current.CancellationToken));

        // Assert
        Assert.Single(auth.Numbers.Ach);
        Assert.Equal(2, handler.CallsTo("auth/get").Count());
        Assert.Single(handler.CallsTo("item/public_token/exchange"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exchange.StatusCode);
    }
}

/// <summary>
/// Plaid answers shaped like Plaid's own.
/// </summary>
internal static class PlaidSamples
{
    public static string Auth(string accountId, string account, string routing, bool tokenized, string subtype = "checking")
        => $$"""
        {
          "accounts": [
            { "account_id": "{{accountId}}", "mask": "{{account[^4..]}}", "name": "Plaid Checking", "type": "depository", "subtype": "{{subtype}}" }
          ],
          "numbers": {
            "ach": [
              { "account_id": "{{accountId}}", "account": "{{account}}", "routing": "{{routing}}", "wire_routing": "{{routing}}", "is_tokenized_account_number": {{(tokenized ? "true" : "false")}} }
            ]
          },
          "request_id": "req-auth"
        }
        """;
}

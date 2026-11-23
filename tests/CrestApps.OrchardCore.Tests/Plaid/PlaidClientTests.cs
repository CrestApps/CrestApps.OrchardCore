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
    public async Task CreateLinkToken_ForTransactions_SendsTheProductsHistoryAndNoDefaultFilter()
    {
        // Arrange
        var context = new PlaidTestContext();
        context.Handler.Answer("link/token/create", """{"link_token":"link-sandbox-2","request_id":"r"}""");

        // Act
        await context.Client.CreateLinkTokenAsync(FixedPlaidCredentials.Sandbox, new PlaidLinkTokenRequest
        {
            ClientUserId = "opaque",
            Products = ["transactions"],
            AccountFilters = new Dictionary<string, IList<string>>(),
            CountryCodes = ["US", "CA"],
            TransactionsDaysRequested = 5000,
        }, TestContext.Current.CancellationToken);

        // Assert
        var (_, _, body) = Assert.Single(context.Handler.Calls);
        Assert.Equal("transactions", Assert.Single(body["products"]!.AsArray())!.GetValue<string>());
        Assert.Equal(["US", "CA"], body["country_codes"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Equal(730, body["transactions"]!["days_requested"]!.GetValue<int>());
        Assert.Null(body["account_filters"]);
        Assert.Null(body["access_token"]);
    }

    [Fact]
    public async Task CreateLinkToken_InUpdateMode_SendsTheAccessTokenWithoutProductsOrFilters()
    {
        // Arrange
        var context = new PlaidTestContext();
        context.Handler.Answer("link/token/create", """{"link_token":"link-sandbox-3","request_id":"r"}""");

        // Act
        await context.Client.CreateLinkTokenAsync(FixedPlaidCredentials.Sandbox, new PlaidLinkTokenRequest
        {
            ClientUserId = "opaque",
            AccessToken = "access-1",
            Products = ["transactions"],
        }, TestContext.Current.CancellationToken);

        // Assert
        var (_, _, body) = Assert.Single(context.Handler.Calls);
        Assert.Equal("access-1", body["access_token"]!.GetValue<string>());
        Assert.Null(body["products"]);
        Assert.Null(body["account_filters"]);
    }

    [Fact]
    public async Task GetAccounts_ReadsEveryAccountOfTheItem_WithBalances()
    {
        // Arrange
        var context = new PlaidTestContext();
        context.Handler.Answer("accounts/get", """
            {
              "accounts": [
                { "account_id": "acc-1", "mask": "0000", "name": "Checking", "official_name": "Plaid Gold Checking", "type": "depository", "subtype": "checking", "balances": { "available": 100.5, "current": 110, "limit": null, "iso_currency_code": "USD" } },
                { "account_id": "acc-2", "mask": "1111", "name": "Card", "type": "credit", "subtype": "credit card", "balances": { "current": 410.25, "limit": 2000, "iso_currency_code": "USD" } }
              ],
              "item": { "item_id": "item-1", "institution_id": "ins_109508", "webhook": "https://example.com/plaid/webhook" },
              "request_id": "r"
            }
            """);

        // Act
        var accounts = await context.Client.GetAccountsAsync(FixedPlaidCredentials.Sandbox, "access-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["acc-1", "acc-2"], accounts.Accounts.Select(account => account.AccountId));
        Assert.Equal(100.5m, accounts.Accounts[0].Balances.Available);
        Assert.Equal("Plaid Gold Checking", accounts.Accounts[0].OfficialName);
        Assert.Equal(2000m, accounts.Accounts[1].Balances.Limit);
        Assert.Equal("ins_109508", accounts.Item.InstitutionId);
        var (_, request, body) = Assert.Single(context.Handler.Calls);
        Assert.Equal("access-1", body["access_token"]!.GetValue<string>());
        Assert.True(request.Options.TryGetValue(PlaidClient.RetryableKey, out var retryable) && retryable);
    }

    [Fact]
    public async Task SyncTransactions_SendsTheCursorAndOptions_AndReadsThePage()
    {
        // Arrange
        var context = new PlaidTestContext();
        context.Handler.Answer("transactions/sync", """
            {
              "added": [ { "transaction_id": "t-1", "account_id": "acc-1", "amount": 12.34, "iso_currency_code": "USD", "date": "2026-11-20", "authorized_date": "2026-11-19", "name": "Coffee", "merchant_name": "Cafe", "original_description": "CAFE 123", "pending": false, "payment_channel": "in store" } ],
              "modified": [],
              "removed": [ { "transaction_id": "t-0", "account_id": "acc-1" } ],
              "accounts": [],
              "next_cursor": "cursor-2",
              "has_more": true,
              "transactions_update_status": "HISTORICAL_UPDATE_COMPLETE",
              "request_id": "r"
            }
            """);

        // Act
        var page = await context.Client.SyncTransactionsAsync(FixedPlaidCredentials.Sandbox, new PlaidTransactionsSyncRequest
        {
            AccessToken = "access-1",
            Cursor = "cursor-1",
            Count = 1000,
            AccountId = "acc-1",
            IncludeOriginalDescription = true,
        }, TestContext.Current.CancellationToken);

        // Assert
        var added = Assert.Single(page.Added);
        Assert.Equal(12.34m, added.Amount);
        Assert.Equal(new DateOnly(2026, 11, 20), added.Date);
        Assert.Equal(new DateOnly(2026, 11, 19), added.AuthorizedDate);
        Assert.Equal("CAFE 123", added.OriginalDescription);
        Assert.Equal("t-0", Assert.Single(page.Removed).TransactionId);
        Assert.Equal("cursor-2", page.NextCursor);
        Assert.True(page.HasMore);
        var (_, request, body) = Assert.Single(context.Handler.Calls);
        Assert.Equal("cursor-1", body["cursor"]!.GetValue<string>());
        Assert.Equal(500, body["count"]!.GetValue<int>());
        Assert.Equal("acc-1", body["options"]!["account_id"]!.GetValue<string>());
        Assert.True(body["options"]!["include_original_description"]!.GetValue<bool>());
        Assert.True(request.Options.TryGetValue(PlaidClient.RetryableKey, out var retryable) && retryable);
    }

    [Fact]
    public async Task GetInstitution_ReadsTheBanksName()
    {
        // Arrange
        var context = new PlaidTestContext();
        context.Handler.Answer("institutions/get_by_id", """{"institution":{"institution_id":"ins_1","name":"First Platypus Bank"},"request_id":"r"}""");

        // Act
        var institution = await context.Client.GetInstitutionAsync(FixedPlaidCredentials.Sandbox, "ins_1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("First Platypus Bank", institution.Name);
        var (_, _, body) = Assert.Single(context.Handler.Calls);
        Assert.Equal("US", Assert.Single(body["country_codes"]!.AsArray())!.GetValue<string>());
    }

    [Fact]
    public async Task UpdateItemWebhook_SendsTheAccessTokenAndTheNewUrl_AndReadsTheItem()
    {
        // Arrange
        var context = new PlaidTestContext();
        context.Handler.Answer("item/webhook/update", """{"item":{"item_id":"item-1","institution_id":"ins_1","webhook":"https://shop.example.com/plaid/webhook"},"request_id":"r"}""");

        // Act
        var item = await context.Client.UpdateItemWebhookAsync(FixedPlaidCredentials.Sandbox, "access-1", "https://shop.example.com/plaid/webhook", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("item-1", item.ItemId);
        Assert.Equal("https://shop.example.com/plaid/webhook", item.Webhook);
        var (_, request, body) = Assert.Single(context.Handler.Calls);
        Assert.Equal("access-1", body["access_token"]!.GetValue<string>());
        Assert.Equal("https://shop.example.com/plaid/webhook", body["webhook"]!.GetValue<string>());
        Assert.True(request.Options.TryGetValue(PlaidClient.RetryableKey, out var retryable) && retryable);
    }

    [Theory]
    [InlineData("plaid/webhook")]
    [InlineData("ftp://shop.example.com/plaid/webhook")]
    public async Task UpdateItemWebhook_RefusesAUrlThatIsNotAnAbsoluteWebAddress(string webhookUrl)
    {
        // Arrange
        var context = new PlaidTestContext();

        // Act and assert
        await Assert.ThrowsAsync<ArgumentException>(() => context.Client.UpdateItemWebhookAsync(FixedPlaidCredentials.Sandbox, "access-1", webhookUrl, TestContext.Current.CancellationToken));
        Assert.Empty(context.Handler.Calls);
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

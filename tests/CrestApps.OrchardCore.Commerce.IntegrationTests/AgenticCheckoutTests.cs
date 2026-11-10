using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;

namespace CrestApps.OrchardCore.Commerce.IntegrationTests;

/// <summary>
/// An AI agent platform buying over the Agentic Commerce Protocol, end to end, with the test payment provider; and
/// the guarantees that matter most to a buyer: a checkout is paid once, however many times, and however quickly, the
/// agent asks to complete it.
/// </summary>
[Collection(CommerceApiCollection.Name)]
public sealed class AgenticCheckoutTests
{
    private const string ApiVersion = "2026-04-17";

    private static readonly object _address = new
    {
        name = "Agent Buyer",
        line_one = "500 Pine St",
        city = "Seattle",
        state = "WA",
        country = "US",
        postal_code = "98101",
    };

    private readonly CommerceApiFixture _fixture;

    public AgenticCheckoutTests(CommerceApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AnAgent_ChecksOut_AndTheOrderIsPaidOnce()
    {
        using var agent = await CreatePlatformAsync();
        var id = await CreateReadyCheckoutAsync(agent);

        var completed = await agent.SendAsync("checkout_sessions/" + id + "/complete", Payment("tok_test_success"));

        Assert.Equal(200, completed.Status);
        Assert.Equal("completed", completed.Json["status"].GetValue<string>());
        Assert.NotNull(completed.Json["order"]?["id"]);

        // Completing a completed checkout never pays again.
        var again = await agent.SendAsync("checkout_sessions/" + id + "/complete", Payment("tok_test_success"));

        Assert.NotEqual(200, again.Status);

        await AssertPaidOnceAsync(id);
    }

    [Fact]
    public async Task ParallelCompletes_WithDifferentKeys_PayOnce()
    {
        using var agent = await CreatePlatformAsync();
        var id = await CreateReadyCheckoutAsync(agent);

        var answers = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            agent.SendAsync("checkout_sessions/" + id + "/complete", Payment("tok_test_success"))));

        var completed = answers.Where(answer => answer.Status == 200 && answer.Json?["status"]?.GetValue<string>() == "completed").ToList();

        Assert.True(completed.Count >= 1, string.Join(Environment.NewLine, answers.Select(answer => answer.ToString())));
        Assert.Single(completed.Select(answer => answer.Json["order"]["id"].GetValue<string>()).Distinct());

        await AssertPaidOnceAsync(id);
    }

    [Fact]
    public async Task ParallelCompletes_WithTheSameKey_PayOnce_AndReplayTheAnswer()
    {
        using var agent = await CreatePlatformAsync();
        var id = await CreateReadyCheckoutAsync(agent);
        var key = Guid.NewGuid().ToString();

        var answers = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            agent.SendAsync("checkout_sessions/" + id + "/complete", Payment("tok_test_success"), key)));

        // One request runs; the others are told it's still running, or get its answer replayed.
        Assert.All(answers, answer => Assert.True(answer.Status is 200 or 409, answer.ToString()));
        Assert.Contains(answers, answer => answer.Status == 200);

        var replay = await agent.SendAsync("checkout_sessions/" + id + "/complete", Payment("tok_test_success"), key);

        Assert.Equal(200, replay.Status);
        Assert.Equal("completed", replay.Json["status"].GetValue<string>());

        await AssertPaidOnceAsync(id);
    }

    [Fact]
    public async Task ADeclinedPayment_ThenASuccess_PaysOnce()
    {
        using var agent = await CreatePlatformAsync();
        var id = await CreateReadyCheckoutAsync(agent);

        var declined = await agent.SendAsync("checkout_sessions/" + id + "/complete", Payment("tok_test_decline"));

        Assert.NotEqual("completed", declined.Json?["status"]?.GetValue<string>());

        var completed = await agent.SendAsync("checkout_sessions/" + id + "/complete", Payment("tok_test_success"));

        Assert.Equal("completed", completed.Json["status"].GetValue<string>());

        await AssertPaidOnceAsync(id);
    }

    [Fact]
    public async Task APlatform_NeverSeesAnotherPlatformsCheckout()
    {
        using var owner = await CreatePlatformAsync();
        using var stranger = await CreatePlatformAsync();
        var id = await CreateReadyCheckoutAsync(owner);

        Assert.Equal(404, (await stranger.GetAsync("checkout_sessions/" + id)).Status);
        Assert.Equal(404, (await stranger.SendAsync("checkout_sessions/" + id + "/complete", Payment("tok_test_success"))).Status);
        Assert.Equal(200, (await owner.GetAsync("checkout_sessions/" + id)).Status);
    }

    private static object Payment(string token)
        => new
        {
            payment_data = new
            {
                handler_id = "test",
                instrument = new
                {
                    type = "card",
                    credential = new { type = "test", token },
                },
            },
        };

    // A checkout with an item, the buyer and a shipping address: everything but the payment.
    private async Task<string> CreateReadyCheckoutAsync(AgentClient agent)
    {
        var itemId = await FindItemIdAsync();
        var created = await agent.SendAsync("checkout_sessions", new
        {
            line_items = new[] { new { id = itemId, quantity = 1 } },
            currency = "usd",
            buyer = new { email = "agent.buyer@example.com", first_name = "Agent", last_name = "Buyer" },
            fulfillment_details = new { name = "Agent Buyer", email = "agent.buyer@example.com", address = _address },
        });

        Assert.True(created.Status == 201 || created.Status == 200, created.ToString());
        Assert.Equal("ready_for_payment", created.Json["status"].GetValue<string>());

        return created.Json["id"].GetValue<string>();
    }

    private async Task AssertPaidOnceAsync(string checkoutId)
    {
        await _fixture.InTenantAsync(async services =>
        {
            var checkout = await services.GetRequiredService<IAgentCheckoutStore>().FindByIdAsync(checkoutId);

            Assert.NotNull(checkout);
            Assert.Equal(AgentCheckoutStatus.Completed, checkout.Status);

            var attempts = (await services.GetRequiredService<IPaymentAttemptStore>().GetBySessionAsync(checkout.CheckoutSessionId)).ToList();
            var succeeded = attempts.Where(attempt => attempt.State == PaymentAttemptState.Succeeded).ToList();

            Assert.Single(succeeded);
            Assert.DoesNotContain(attempts, attempt => attempt.State is PaymentAttemptState.Created or PaymentAttemptState.Pending);

            var order = await services.GetRequiredService<IOrderStore>().FindByIdAsync(checkout.OrderId);

            Assert.NotNull(order);
            Assert.Equal(order.Total ?? 0m, succeeded[0].ExpectedAmount + succeeded[0].ExpectedTaxAmount);
        });
    }

    private async Task<string> FindItemIdAsync()
    {
        using var client = _fixture.CreateClient();
        var productId = await GuestCheckoutApiTests.FindPurchasableAsync(client);
        var product = await client.GetAsync("products/" + productId);

        return product.Json["sku"]?.GetValue<string>() is { Length: > 0 } sku ? sku : productId;
    }

    private async Task<AgentClient> CreatePlatformAsync()
    {
        var apiKey = AgentPlatformApiKeys.Create();

        await _fixture.InTenantAsync(async services =>
        {
            await services.GetRequiredService<IAgentPlatformStore>().CreateAsync(new AgentPlatform
            {
                ItemId = IdGenerator.GenerateId(),
                Name = "Test agent " + Guid.NewGuid().ToString("N")[..6],
                Enabled = true,
                TestMode = true,
                Protocols = [AgenticCommerceConstants.Protocols.Acp],
                ApiKeyHash = AgentPlatformApiKeys.Hash(apiKey),
                ApiKeyHint = AgentPlatformApiKeys.Hint(apiKey),
                CreatedUtc = DateTime.UtcNow,
            });
        });

        return new AgentClient(new HttpClient { BaseAddress = new Uri(_fixture.BaseAddress, "acp/") }, apiKey);
    }

    /// <summary>
    /// An agent platform calling the store's ACP endpoints with its API key.
    /// </summary>
    private sealed class AgentClient : IDisposable
    {
        private readonly HttpClient _http;

        public AgentClient(HttpClient http, string apiKey)
        {
            _http = http;
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _http.DefaultRequestHeaders.Add("API-Version", ApiVersion);
        }

        public Task<ApiResponse> GetAsync(string path)
            => SendAsync(HttpMethod.Get, path, body: null, idempotencyKey: null);

        public Task<ApiResponse> SendAsync(string path, object body, string idempotencyKey = null)
            => SendAsync(HttpMethod.Post, path, body, idempotencyKey ?? Guid.NewGuid().ToString());

        private async Task<ApiResponse> SendAsync(HttpMethod method, string path, object body, string idempotencyKey)
        {
            using var request = new HttpRequestMessage(method, path);

            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            if (idempotencyKey is not null)
            {
                request.Headers.Add("Idempotency-Key", idempotencyKey);
            }

            using var response = await _http.SendAsync(request, TestContext.Current.CancellationToken);
            var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            return new ApiResponse((int)response.StatusCode, string.IsNullOrEmpty(text) ? null : JsonNode.Parse(text), text);
        }

        public void Dispose()
            => _http.Dispose();
    }
}

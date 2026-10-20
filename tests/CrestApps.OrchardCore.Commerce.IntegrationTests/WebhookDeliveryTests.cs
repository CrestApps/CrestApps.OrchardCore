using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Commerce.Webhooks;
using CrestApps.OrchardCore.Commerce.Webhooks.Models;
using CrestApps.OrchardCore.Commerce.Webhooks.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;

namespace CrestApps.OrchardCore.Commerce.IntegrationTests;

/// <summary>
/// A webhook receiving the events of an order placed over the API, each signed with the webhook's secret.
/// </summary>
[Collection(CommerceApiCollection.Name)]
public sealed class WebhookDeliveryTests
{
    private readonly CommerceApiFixture _fixture;

    public WebhookDeliveryTests(CommerceApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AnOrderPaidOverTheApi_IsPostedToTheWebhook_Signed()
    {
        using var receiver = new Receiver();
        var subscriptionId = IdGenerator.GenerateId();
        var secret = await _fixture.InTenantAsync(async services =>
        {
            var dispatcher = services.GetRequiredService<WebhookDispatcher>();
            var subscription = new WebhookSubscription
            {
                ItemId = subscriptionId,
                Name = "Test receiver",
                Url = receiver.Url,
                Events = [CommerceWebhookEvents.OrderCreated, CommerceWebhookEvents.OrderPaid],
                ProtectedSecret = dispatcher.NewProtectedSecret(),
                CreatedUtc = DateTime.UtcNow,
            };

            await services.GetRequiredService<IWebhookSubscriptionStore>().CreateAsync(subscription);

            return dispatcher.GetSecret(subscription);
        });

        try
        {
            var orderId = await GuestCheckoutApiTests.CheckOutAsync(_fixture);

            var paid = await receiver.WaitForAsync(CommerceWebhookEvents.OrderPaid, TimeSpan.FromSeconds(20));
            var created = await receiver.WaitForAsync(CommerceWebhookEvents.OrderCreated, TimeSpan.FromSeconds(5));
            var body = JsonNode.Parse(paid.Body);

            Assert.Equal(orderId, body["data"]["order"]["id"].GetValue<string>());
            Assert.Equal("paid", body["data"]["order"]["status"].GetValue<string>());
            Assert.Equal(body["id"].GetValue<string>(), paid.Headers["X-Webhook-Id"]);
            Assert.Equal(WebhookDispatcher.Sign(secret, long.Parse(paid.Headers["X-Webhook-Timestamp"]), paid.Body), paid.Headers["X-Webhook-Signature"]);
            Assert.NotEqual(paid.Headers["X-Webhook-Id"], created.Headers["X-Webhook-Id"]);
        }
        finally
        {
            await _fixture.InTenantAsync(async services =>
            {
                var store = services.GetRequiredService<IWebhookSubscriptionStore>();

                if (await store.FindByIdAsync(subscriptionId) is { } subscription)
                {
                    await store.DeleteAsync(subscription);
                }
            });
        }
    }

    private sealed record Received(string Event, string Body, IReadOnlyDictionary<string, string> Headers);

    // A receiver on a free loopback port that keeps what it's sent and answers 200.
    private sealed class Receiver : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly ConcurrentQueue<Received> _received = new();
        private readonly CancellationTokenSource _stop = new();

        public Receiver()
        {
            var port = FreePort();
            Url = $"http://127.0.0.1:{port}/hooks/";
            _listener.Prefixes.Add(Url);
            _listener.Start();
            _ = Task.Run(LoopAsync);
        }

        public string Url { get; }

        public async Task<Received> WaitForAsync(string eventName, TimeSpan timeout)
        {
            var until = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < until)
            {
                var match = _received.FirstOrDefault(entry => entry.Event == eventName);

                if (match is not null)
                {
                    return match;
                }

                await Task.Delay(200, TestContext.Current.CancellationToken);
            }

            throw new TimeoutException($"No '{eventName}' arrived; got: {string.Join(", ", _received.Select(entry => entry.Event))}.");
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Close();
        }

        private async Task LoopAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                HttpListenerContext context;

                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception) when (_stop.IsCancellationRequested)
                {
                    return;
                }

                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();
                var headers = context.Request.Headers.AllKeys.Where(key => key is not null).ToDictionary(key => key, key => context.Request.Headers[key], StringComparer.OrdinalIgnoreCase);

                _received.Enqueue(new Received(context.Request.Headers["X-Webhook-Event"], body, headers));
                context.Response.StatusCode = 200;
                context.Response.Close();
            }
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            return port;
        }
    }
}
